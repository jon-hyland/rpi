using Microsoft.Extensions.Configuration;
using Rpi.Common.Error;
using Rpi.Common.Http;
using Rpi.Common.Output;
using Rpi.Common.Service;
using Rpi.Gpio.Configuration;
using Rpi.Gpio.Error;
using Rpi.Gpio.Handlers;
using Rpi.Gpio.Health;
using Rpi.Gpio.IO;
using Rpi.Gpio.Output;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Unosquare.RaspberryIO;
using Unosquare.WiringPi;

namespace Rpi.Gpio
{
    /// <summary>
    /// Primary service class.  Maintains global objects and threads.
    /// </summary>
    public class Main
    {
        //singletons
        private Config _config;
        private IErrorLogger _errorCache;
        private IErrorHandler _errorHandler;
        private ILogger _logger;
        private ServiceState _serviceState;
        private ServiceStats _serviceStats;
        private Heartbeat _heartbeat;
        private PiInfo _piInfo;
        private GpioManager _gpio;

        //private
        private static DateTime _startTime = DateTime.Now;
        private Thread _healthThread;
        private readonly List<IStatsWriter> _statsWriters = new List<IStatsWriter>();
        private SimpleHttpListener _httpListener;
        private readonly AutoResetEvent _exitSignal = new AutoResetEvent(false);

        //public
        public static DateTime StartTime => _startTime;
        public static TimeSpan RunningTime => DateTime.Now - _startTime;

        /// <summary>
        /// Starts the service.
        /// </summary>
        public void Start()
        {
            try
            {
                //message
                _logger = Log.Instance;
                _logger.WriteMessage("Service", "==========================================");
                _logger.WriteMessage("Service", "Starting service..");

                //start time
                _startTime = DateTime.Now;

                //events
                Console.CancelKeyPress += Console_CancelKeyPress;

                //config
                _logger.WriteMessage("Service", "Loading configuration..");
                IConfigurationRoot root = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("Config.json", optional: true, reloadOnChange: false)
                    .Build();
                _config = new Config(root);
                _logger.WriteMessage("Service", $"Service version is {_config.ServiceVersion}");

                //init pi (if linux)
                if (_config.IsLinux)
                    Pi.Init<BootstrapWiringPi>();

                //singletons
                _logger.WriteMessage("Service", "Creating support structures..");
                ((Log)Log.Instance).Initialize(_config.IsWindows ? "C:\\Temp\\" : "/var/log/", _config.ServiceVersion);
                _errorCache = new ErrorCache(null, _config.ErrorRetention);
                _errorHandler = new ErrorHandler(_errorCache, _logger);
                _serviceState = new ServiceState(_errorHandler, _logger, ServiceStateType.Down);
                _serviceStats = new ServiceStats(_errorHandler);
                _heartbeat = new Heartbeat(_errorHandler, _config, _logger, _serviceStats, _serviceState);
                _piInfo = new PiInfo(_config);
                _gpio = new GpioManager(_errorHandler, _config);

                //stats writers
                _statsWriters.Add(_serviceState);
                _statsWriters.Add(_serviceStats);
                _statsWriters.Add(_gpio);
                _statsWriters.Add(_piInfo);
                _statsWriters.Add((IStatsWriter)_errorCache);

                //start health thread
                _logger.WriteMessage("Service", "Starting health thread..");
                _healthThread = new Thread(Health_Thread)
                {
                    IsBackground = true
                };
                _healthThread.Start();

                //finish init on another thread, finish start
                Task.Run(() =>
                {
                    //start gpio loop
                    _gpio.Initialize();
                    
                    //wait for ip assignment
                    WaitForIPAssignment();

                    //start heatbeat timer
                    _heartbeat.Start();

                    //start http listener
                    StartHttpListener();
                });

                //log start operation
                _serviceStats.LogOperation("ServiceStart", DateTime.Now.Subtract(_startTime));

                //wait for exit signal
                _exitSignal.WaitOne();
            }
            catch (Exception ex)
            {
                if (_errorHandler != null)
                    _errorHandler.LogError(ex);
                else
                    Console.WriteLine(ex);
            }
        }

        /// <summary>
        /// Waits and blocks until device has a valid IP assignment.
        /// </summary>
        private void WaitForIPAssignment()
        {
            try
            {
                //message
                _logger.WriteMessage("Service", $"Waiting for IP assignment..");

                //loop forever
                DateTime lastMessage = DateTime.MinValue;
                while (true)
                {
                    try
                    {
                        //calc time since last message
                        TimeSpan timeSinceLastMessage = DateTime.Now.Subtract(lastMessage);

                        //find default interface
                        List<NetworkInterface> interfaces = NetworkInterface.GetAllNetworkInterfaces()
                            .Where(i => i.NetworkInterfaceType == NetworkInterfaceType.Ethernet || i.NetworkInterfaceType == NetworkInterfaceType.GigabitEthernet)
                            .Where(i => i.OperationalStatus == OperationalStatus.Up)
                            .ToList();
                        NetworkInterface ni = null;
                        foreach (NetworkInterface i in interfaces)
                        {
                            foreach (string partial in _config.InterfaceNames)
                            {
                                if (i.Name.Contains(partial, StringComparison.InvariantCultureIgnoreCase))
                                {
                                    ni = i;
                                    break;
                                }
                            }
                            if (ni != null)
                                break;
                        }

                        //have interface?
                        if (ni != null)
                        {
                            //message
                            _logger.WriteMessage("Service", $"Found primary network interface with name '{ni.Name}'..");

                            //get first valid address
                            IPAddress ip = ni.GetIPProperties().UnicastAddresses
                                .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork)
                                .Where(a => !a.Address.ToString().StartsWith("169."))
                                .FirstOrDefault()?.Address;

                            //have address?
                            if (ip != null)
                            {
                                _logger.WriteMessage("Service", $"Found valid IP assignment of '{ip}'");
                                _logger.WriteMessage("Service", $"Allowing HTTP listener to start..");
                                return;
                            }
                            else
                            {
                                if (timeSinceLastMessage.TotalSeconds >= 60)
                                {
                                    _logger.WriteMessage("Service", "Waiting for IP assignment..");
                                    lastMessage = DateTime.Now;
                                }
                            }
                        }

                        //no interface found (this shouldn't happen)
                        else
                        {
                            if (timeSinceLastMessage.TotalSeconds >= 60)
                            {
                                _logger.WriteMessage("Service", "Waiting for IP assignment.. cannot find primary network interface");
                                lastMessage = DateTime.Now;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _errorHandler?.LogError(ex);
                    }

                    //sleep
                    Thread.Sleep(1000);
                }
            }
            catch (Exception ex)
            {
                _errorHandler?.LogError(ex);
            }
        }

        /// <summary>
        /// Starts the HTTP listener.
        /// </summary>
        private void StartHttpListener()
        {
            try
            {
                //create http listener
                _logger.WriteMessage("Service", $"Creating HTTP listener..");
                _httpListener = new SimpleHttpListener(_config.ListenPort, _errorHandler);

                //add statistics handler
                _logger.WriteMessage("Service", $"Registering 'Statistics' HTTP handler..");
                StatisticsHandler statisticsHandler = new StatisticsHandler(_errorHandler, _config, _logger, _serviceStats, _statsWriters);
                _httpListener.RegisterHandler("statistics", statisticsHandler);

                //add config handler
                _logger.WriteMessage("Service", $"Registering 'Config' HTTP handler..");
                ConfigHandler configHandler = new ConfigHandler(_errorHandler, _config, _logger, _serviceStats);
                _httpListener.RegisterHandler("config", configHandler);

                //add gpio handler
                _logger.WriteMessage("Service", $"Registering 'GPIO' HTTP handler..");
                GpioHandler gpioHandler = new GpioHandler(_errorHandler, _config, _logger, _serviceStats, _gpio);
                _httpListener.RegisterHandler("gpio", gpioHandler);

                //pre-init
                _logger.WriteMessage("Service", $"Running pre-init handler functions..");
                foreach (IHttpHandler handler in _httpListener.GetHandlers())
                    handler.PreInitialize();

                //start http listener
                _logger.WriteMessage("Service", $"Starting HTTP listener on port {_config.ListenPort}..");
                _httpListener.Start();

                //pre-init
                _logger.WriteMessage("Service", $"Running post-init handler functions..");
                foreach (IHttpHandler handler in _httpListener.GetHandlers())
                    handler.PostInitialize();

                //success
                _logger.WriteMessage("Service", $"HTTP listener successfully connected");

                //state
                _serviceState.SetState(ServiceStateType.Up);
            }
            catch (Exception ex)
            {
                _errorHandler?.LogError(ex);
            }
        }

        /// <summary>
        /// Fired when cancel keystroke received.
        /// </summary>
        private void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            try
            {
                //message
                _logger.WriteMessage("Service", "Cancel keystroke detected..");

                //have listener?
                if (_httpListener != null)
                {
                    //stop listener
                    _logger.WriteMessage("Service", "Attempting HTTP listener stop..");
                    _httpListener.Stop().Wait();
                }

                //cancel regular flag
                e.Cancel = true;

                //set signal
                _exitSignal.Set();

                //message
                _logger.WriteMessage("Service", "Exit signal sent");
            }
            catch (Exception ex)
            {
                _errorHandler?.LogError(ex);
            }
        }

        /// <summary>
        /// The service's health thread, ensures other critical threads are running.
        /// </summary>
        private void Health_Thread()
        {
            try
            {
                //loop forever
                while (true)
                {
                    try
                    {
                        //do stuff
                        _gpio.Maintenance();
                    }
                    catch (Exception ex)
                    {
                        _errorHandler.LogError(ex);
                    }

                    //sleep
                    Thread.Sleep(TimeSpan.FromMilliseconds(1000));
                }
            }
            catch (Exception ex)
            {
                _errorHandler?.LogError(ex);
            }
        }

    }
}
