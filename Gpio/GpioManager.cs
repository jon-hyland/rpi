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
        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
        private readonly IGpioPin[] _pins = new IGpioPin[32];
        private readonly bool[] _values = new bool[32];

        /// <summary>
        /// Class constructor.
        /// </summary>
        public GpioManager(IErrorHandler errorHandler)
        {
            //vars
            _errorHandler = errorHandler;

            //init pi
            Pi.Init<BootstrapWiringPi>();
            _pins[0] = null;
            _pins[1] = null;
            _pins[2] = Pi.Gpio[P1.Pin03];
            _pins[3] = Pi.Gpio[P1.Pin05];
            _pins[4] = Pi.Gpio[P1.Pin07];
            _pins[5] = Pi.Gpio[P1.Pin26];
            _pins[6] = Pi.Gpio[P1.Pin31];
            _pins[7] = Pi.Gpio[P1.Pin26];
            _pins[8] = Pi.Gpio[P1.Pin24];
            _pins[9] = Pi.Gpio[P1.Pin21];
            _pins[10] = Pi.Gpio[P1.Pin19];
            _pins[11] = Pi.Gpio[P1.Pin23];
            _pins[12] = Pi.Gpio[P1.Pin32];
            _pins[13] = Pi.Gpio[P1.Pin33];
            _pins[14] = null;
            _pins[15] = null;
            _pins[16] = Pi.Gpio[P1.Pin36];
            _pins[17] = Pi.Gpio[P1.Pin11];
            _pins[18] = Pi.Gpio[P1.Pin12];
            _pins[19] = Pi.Gpio[P1.Pin35];
            _pins[20] = Pi.Gpio[P1.Pin38];
            _pins[21] = Pi.Gpio[P1.Pin40];
            _pins[22] = Pi.Gpio[P1.Pin15];
            _pins[23] = Pi.Gpio[P1.Pin16];
            _pins[24] = Pi.Gpio[P1.Pin18];
            _pins[25] = Pi.Gpio[P1.Pin22];
            _pins[26] = Pi.Gpio[P1.Pin37];
            _pins[27] = Pi.Gpio[P1.Pin13];
            _pins[28] = null;
            _pins[29] = null;
            _pins[30] = null;
            _pins[31] = null;

            for (int i = 0; i < 32; i++)
            {
                if (_pins[i] != null)
                {
                    _pins[i].PinMode = GpioPinDriveMode.Input;
                    _pins[i].Write(false);
                }
            }

            for (int i = 0; i < 32; i++)
                if (_pins[i] != null)
                    _pins[i].PinMode = GpioPinDriveMode.Input;

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
                    _lock.EnterWriteLock();
                    try
                    {
                        for (int i = 0; i < 32; i++)
                        {
                            if (_pins[i] == null)
                                continue;
                            _values[i] = _pins[i].Read();
                        }
                    }
                    finally
                    {
                        _lock.ExitWriteLock();
                    }
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
            _lock.EnterReadLock();
            try
            {
                writer.WriteStartObject("gpio");
                writer.WriteStartArray("values");
                foreach (bool value in _values)
                    writer.WriteValue(value ? 1 : 0);
                writer.WriteEndArray();
                writer.WriteEndObject();
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
    }
}
