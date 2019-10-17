using Rpi.Error;
using Rpi.Json;
using Rpi.Output;
using System;
using System.Threading;
using Unosquare.RaspberryIO;
using Unosquare.RaspberryIO.Abstractions;
using Unosquare.WiringPi;

namespace Rpi.Gpio
{
    /// <summary>
    /// Manages the GPIO interface of the current RPI.
    /// </summary>
    public class GpioManager : IStatsWriter
    {
        //private
        private readonly IErrorHandler _errorHandler = null;
        private Thread _thread = null;
        private readonly ManualResetEventSlim _signal = new ManualResetEventSlim(false);
        private bool[] _gpio = new bool[24];

        /// <summary>
        /// Class constructor.
        /// </summary>
        public GpioManager(IErrorHandler errorHandler)
        {
            //vars
            _errorHandler = errorHandler;

            //init pi
            Pi.Init<BootstrapWiringPi>();

            //start thread
            _thread = new Thread(new ThreadStart(Polling_Thread))
            {
                IsBackground = true
            };
            _thread.Start();
        }

        /// <summary>
        /// Polling thread used to push/pull data from GPIO interface.
        /// </summary>
        private void Polling_Thread()
        {
            _signal.Set();
            while (true)
            {
                try
                {
                    Pi.Gpio[P1.Pin03].PinMode = GpioPinDriveMode.Input;
                    bool on = Pi.Gpio[P1.Pin03].Read();

                    lock (_gpio)
                    {
                        _gpio[2] = on;
                    }

                    Log.WriteMessage("Gpio", $"Pin 3: {(on ? "On" : "Off")}");
                }
                catch (Exception ex)
                {
                    _errorHandler?.LogError(ex);
                }

                Thread.Sleep(100);
            }
        }

        /// <summary>
        /// Called by service's health timer.
        /// </summary>
        public void Maintenance()
        {
            try
            {
                if (_signal.Wait(1000))
                {
                    if ((_thread == null) || (!_thread.IsAlive))
                    {
                        _thread = new Thread(new ThreadStart(Polling_Thread))
                        {
                            IsBackground = true
                        };
                        _thread.Start();
                        throw new Exception("GPIO thread died and had to be started");
                    }
                }
            }
            catch (Exception ex)
            {
                _errorHandler?.LogError(ex);
            }
        }

        /// <summary>
        /// Writes runtime stats.
        /// </summary>
        public void WriteRuntimeStatistics(SimpleJsonWriter writer)
        {
            writer.WriteStartObject("gpio");
            writer.WriteStartArray("values");
            lock (_gpio)
            {
                foreach (bool value in _gpio)
                    writer.WriteValue(value ? 1 : 0);
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
    }
}
