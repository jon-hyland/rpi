using Microsoft.Extensions.Configuration;
using Rpi.Error;
using Rpi.Gpio;
using Rpi.Handlers;
using Rpi.Health;
using Rpi.Http;
using Rpi.Output;
using Rpi.Service;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Rpi
{
    /// <summary>
    /// Primary service class.  Maintains global objects and threads.
    /// </summary>
    public class Main
    {
        //singletons
        private IConfig _config;
        private IErrorLogger _errorCache;
        private IErrorHandler _errorHandler;
        private ServiceState _serviceState;
        private ServiceStats _serviceStats;
        private Heartbeat _heartbeat;
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
                Log.WriteMessage("Service", "==========================================");
                Log.WriteMessage("Service", "Starting service..");

                //start time
                _startTime = DateTime.Now;

                //events
                Console.CancelKeyPress += Console_CancelKeyPress;

                //config
                Log.WriteMessage("Service", "Loading configuration..");
                IConfigurationRoot root = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("Config.json", optional: true, reloadOnChange: false)
                    .Build();
                _config = new Config(root);
                Log.WriteMessage("Service", $"Service version is {_config.ServiceVersion}");

                //singletons
                Log.WriteMessage("Service", "Creating support structures..");
                Log.Initialize(_config.IsWindows ? "C:\\Temp\\" : "/var/log/", _config.ServiceVersion);
                _errorCache = new ErrorCache(null, _config.ErrorRetention);
                _errorHandler = new ErrorHandler(_errorCache);
                _serviceState = new ServiceState(_errorHandler, ServiceStateType.Down);
                _serviceStats = new ServiceStats(_errorHandler);
                _heartbeat = new Heartbeat(_errorHandler, _config, _serviceStats, _serviceState);
                _gpio = new GpioManager(_errorHandler);

                //stats writers
                _statsWriters.Add(_serviceState);
                _statsWriters.Add(_serviceStats);
                _statsWriters.Add(_gpio);
                _statsWriters.Add((IStatsWriter)_errorCache);

                //start health thread
                Log.WriteMessage("Service", "Starting health thread..");
                _healthThread = new Thread(Health_Thread)
                {
                    IsBackground = true
                };
                _healthThread.Start();

                //finish init on another thread, finish start
                Task.Run(() =>
                {
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
                Log.WriteMessage("Service", $"Waiting for IP assignment..");

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
                            Log.WriteMessage("Service", $"Found primary network interface with name '{ni.Name}'..");

                            //get first valid address
                            IPAddress ip = ni.GetIPProperties().UnicastAddresses
                                .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork)
                                .Where(a => !a.Address.ToString().StartsWith("169."))
                                .FirstOrDefault()?.Address;

                            //have address?
                            if (ip != null)
                            {
                                Log.WriteMessage("Service", $"Found valid IP assignment of '{ip.ToString()}'");
                                Log.WriteMessage("Service", $"Allowing HTTP listener to start..");
                                return;
                            }
                            else
                            {
                                if (timeSinceLastMessage.TotalSeconds >= 60)
                                {
                                    Log.WriteMessage("Service", "Waiting for IP assignment..");
                                    lastMessage = DateTime.Now;
                                }
                            }
                        }

                        //no interface found (this shouldn't happen)
                        else
                        {
                            if (timeSinceLastMessage.TotalSeconds >= 60)
                            {
                                Log.WriteMessage("Service", "Waiting for IP assignment.. cannot find primary network interface");
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
                Log.WriteMessage("Service", $"Creating HTTP listener..");
                _httpListener = new SimpleHttpListener(_config.ListenPort, _errorHandler);

                //add statistics handler
                Log.WriteMessage("Service", $"Registering 'Statistics' HTTP handler..");
                StatisticsHandler statisticsHandler = new StatisticsHandler(_errorHandler, _config, _serviceStats, _statsWriters);
                _httpListener.RegisterHandler("statistics", statisticsHandler);

                //add config handler
                Log.WriteMessage("Service", $"Registering 'Config' HTTP handler..");
                ConfigHandler configHandler = new ConfigHandler(_errorHandler, _config, _serviceStats);
                _httpListener.RegisterHandler("config", configHandler);

                //add gpio handler
                Log.WriteMessage("Service", $"Registering 'GPIO' HTTP handler..");
                GpioHandler gpioHandler = new GpioHandler(_errorHandler, _config, _serviceStats, _gpio);
                _httpListener.RegisterHandler("gpio", gpioHandler);

                //pre-init
                Log.WriteMessage("Service", $"Running pre-init handler functions..");
                foreach (IHttpHandler handler in _httpListener.GetHandlers())
                    handler.PreInitialize();

                //start http listener
                Log.WriteMessage("Service", $"Starting HTTP listener on port {_config.ListenPort.ToString()}..");
                _httpListener.Start();

                //pre-init
                Log.WriteMessage("Service", $"Running post-init handler functions..");
                foreach (IHttpHandler handler in _httpListener.GetHandlers())
                    handler.PostInitialize();

                //success
                Log.WriteMessage("Service", $"HTTP listener successfully connected");

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
                Log.WriteMessage("Service", "Cancel keystroke detected..");

                //have listener?
                if (_httpListener != null)
                {
                    //stop listener
                    Log.WriteMessage("Service", "Attempting HTTP listener stop..");
                    _httpListener.Stop().Wait();
                }

                //cancel regular flag
                e.Cancel = true;

                //set signal
                _exitSignal.Set();

                //message
                Log.WriteMessage("Service", "Exit signal sent");
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
