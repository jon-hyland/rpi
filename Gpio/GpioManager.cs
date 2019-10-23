using Rpi.Configuration;
using Rpi.Error;
using Rpi.Helpers;
using Rpi.Json;
using Rpi.Output;
using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Unosquare.RaspberryIO;
using Unosquare.RaspberryIO.Abstractions;

namespace Rpi.Gpio
{
    /// <summary>
    /// Manages the GPIO interface of the current RPI.
    /// </summary>
    public class GpioManager : IStatsWriter
    {
        //private
        private readonly IErrorHandler _errorHandler = null;
        private readonly IConfig _config = null;
        private Thread _thread = null;
        private readonly ManualResetEventSlim _signal = new ManualResetEventSlim(false);
        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
        private readonly Pin[] _pins = new Pin[32];
        private readonly bool[] _input1 = new bool[8];
        private readonly bool[] _input2 = new bool[8];
        private readonly bool[] _outputRead = new bool[8];
        private readonly bool[] _outputWrite = new bool[8];
        private CpsCalculator _cps = null;

        /// <summary>
        /// Class constructor.
        /// </summary>
        public GpioManager(IErrorHandler errorHandler, IConfig config)
        {
            //vars
            _errorHandler = errorHandler;
            _config = config;
        }

        #region Initialize

        /// <summary>
        /// Initializes the GPIO manager.
        /// </summary>
        public void Initialize()
        {
            try
            {
                //return if not linux
                if (!_config.IsLinux)
                    return;

                //create pins
                _pins[0] = null;
                _pins[1] = null;
                _pins[2] = new Pin(basePin: Pi.Gpio[P1.Pin03], mode: GpioPinDriveMode.Input, bank: BankType.Input1, bit: 0, gpioID: 2, pinID: 3);      // input - read bank 1000, bit 0, always high and can't be used
                _pins[3] = new Pin(basePin: Pi.Gpio[P1.Pin05], mode: GpioPinDriveMode.Input, bank: BankType.Input1, bit: 1, gpioID: 3, pinID: 5);      // input - read bank 1000, bit 1, always high and can't be used
                _pins[4] = new Pin(basePin: Pi.Gpio[P1.Pin07], mode: GpioPinDriveMode.Input, bank: BankType.Input1, bit: 2, gpioID: 4, pinID: 6);      // input - read bank 1000, bit 2
                _pins[5] = new Pin(basePin: Pi.Gpio[P1.Pin29], mode: GpioPinDriveMode.Input, bank: BankType.Input1, bit: 3, gpioID: 5, pinID: 29);     // input - read bank 1000, bit 3
                _pins[6] = new Pin(basePin: Pi.Gpio[P1.Pin31], mode: GpioPinDriveMode.Input, bank: BankType.Input1, bit: 4, gpioID: 6, pinID: 31);     // input - read bank 1000, bit 4
                _pins[7] = new Pin(basePin: Pi.Gpio[P1.Pin26], mode: GpioPinDriveMode.Input, bank: BankType.Input1, bit: 5, gpioID: 7, pinID: 26);     // input - read bank 1000, bit 5
                _pins[8] = new Pin(basePin: Pi.Gpio[P1.Pin24], mode: GpioPinDriveMode.Input, bank: BankType.Input1, bit: 6, gpioID: 8, pinID: 24);     // input - read bank 1000, bit 6
                _pins[9] = new Pin(basePin: Pi.Gpio[P1.Pin21], mode: GpioPinDriveMode.Input, bank: BankType.Input1, bit: 7, gpioID: 9, pinID: 21);     // input - read bank 1000, bit 7
                _pins[10] = new Pin(basePin: Pi.Gpio[P1.Pin19], mode: GpioPinDriveMode.Output, bank: BankType.Output, bit: 0, gpioID: 10, pinID: 19);  // output - write bank 3000, bit 0
                _pins[11] = new Pin(basePin: Pi.Gpio[P1.Pin23], mode: GpioPinDriveMode.Output, bank: BankType.Output, bit: 1, gpioID: 11, pinID: 23);  // output - write bank 3000, bit 1
                _pins[12] = new Pin(basePin: Pi.Gpio[P1.Pin32], mode: GpioPinDriveMode.Output, bank: BankType.Output, bit: 2, gpioID: 12, pinID: 32);  // output - write bank 3000, bit 2, pwm capable, mirrored to 18
                _pins[13] = new Pin(basePin: Pi.Gpio[P1.Pin33], mode: GpioPinDriveMode.Output, bank: BankType.Output, bit: 3, gpioID: 13, pinID: 33);  // output - write bank 3000, bit 3, pwm capable, mirrored to 19
                _pins[14] = null;
                _pins[15] = null;
                _pins[16] = new Pin(basePin: Pi.Gpio[P1.Pin36], mode: GpioPinDriveMode.Output, bank: BankType.Output, bit: 4, gpioID: 16, pinID: 36);  // output - write bank 3000, bit 4
                _pins[17] = new Pin(basePin: Pi.Gpio[P1.Pin11], mode: GpioPinDriveMode.Output, bank: BankType.Output, bit: 5, gpioID: 17, pinID: 11);  // output - write bank 3000, bit 5
                _pins[18] = new Pin(basePin: Pi.Gpio[P1.Pin12], mode: GpioPinDriveMode.Output, bank: BankType.Output, bit: 6, gpioID: 18, pinID: 12);  // output - write bank 3000, bit 6, pwm capable, mirrored to 12
                _pins[19] = new Pin(basePin: Pi.Gpio[P1.Pin35], mode: GpioPinDriveMode.Output, bank: BankType.Output, bit: 7, gpioID: 19, pinID: 35);  // output - write bank 3000, bit 7, pwm capable, mirrored to 13
                _pins[20] = new Pin(basePin: Pi.Gpio[P1.Pin38], mode: GpioPinDriveMode.Input, bank: BankType.Input2, bit: 0, gpioID: 20, pinID: 38);   // input - read bank 2000, bit 0
                _pins[21] = new Pin(basePin: Pi.Gpio[P1.Pin40], mode: GpioPinDriveMode.Input, bank: BankType.Input2, bit: 1, gpioID: 21, pinID: 40);   // input - read bank 2000, bit 1
                _pins[22] = new Pin(basePin: Pi.Gpio[P1.Pin15], mode: GpioPinDriveMode.Input, bank: BankType.Input2, bit: 2, gpioID: 22, pinID: 15);   // input - read bank 2000, bit 2
                _pins[23] = new Pin(basePin: Pi.Gpio[P1.Pin16], mode: GpioPinDriveMode.Input, bank: BankType.Input2, bit: 3, gpioID: 23, pinID: 16);   // input - read bank 2000, bit 3
                _pins[24] = new Pin(basePin: Pi.Gpio[P1.Pin18], mode: GpioPinDriveMode.Input, bank: BankType.Input2, bit: 4, gpioID: 24, pinID: 18);   // input - read bank 2000, bit 4
                _pins[25] = new Pin(basePin: Pi.Gpio[P1.Pin22], mode: GpioPinDriveMode.Input, bank: BankType.Input2, bit: 5, gpioID: 25, pinID: 22);   // input - read bank 2000, bit 5
                _pins[26] = new Pin(basePin: Pi.Gpio[P1.Pin37], mode: GpioPinDriveMode.Input, bank: BankType.Input2, bit: 6, gpioID: 26, pinID: 37);   // input - read bank 2000, bit 6
                _pins[27] = new Pin(basePin: Pi.Gpio[P1.Pin13], mode: GpioPinDriveMode.Input, bank: BankType.Input2, bit: 7, gpioID: 27, pinID: 13);   // input - read bank 2000, bit 7
                _pins[28] = null;
                _pins[29] = null;
                _pins[30] = null;
                _pins[31] = null;

                //initialize pins
                for (int i = 0; i < 32; i++)
                {
                    if (_pins[i] != null)
                    {
                        try
                        {
                            if (_pins[i].Mode == GpioPinDriveMode.Input)
                            {
                                _pins[i].BasePin.PinMode = GpioPinDriveMode.Input;
                                _pins[i].BasePin.InputPullMode = GpioPinResistorPullMode.PullDown;
                            }
                            else if (_pins[i].Mode == GpioPinDriveMode.Output)
                            {
                                _pins[i].BasePin.PinMode = GpioPinDriveMode.Output;
                                _pins[i].BasePin.Write(false);
                            }
                        }
                        catch (Exception ex)
                        {
                            _errorHandler?.LogError(ex);
                        }
                    }
                }

                //cps
                _cps = new CpsCalculator(5);

                //start thread
                _thread = new Thread(new ThreadStart(Polling_Thread))
                {
                    IsBackground = true
                };
                _thread.Start();
            }
            catch (Exception ex)
            {
                _errorHandler?.LogError(ex);
            }
        }

        #endregion

        #region Getters / Setters

        /// <summary>
        /// Gets specified bank as string.
        /// </summary>
        public string GetBank(BankType bank)
        {
            try
            {
                _lock.EnterReadLock();
                try
                {
                    if (bank == BankType.Input1)
                        return BankToString(_input1);
                    else if (bank == BankType.Input2)
                        return BankToString(_input2);
                    else if (bank == BankType.Output)
                        return BankToString(_outputRead);
                    throw new Exception($"Bank not valid");
                }
                finally
                {
                    _lock.ExitReadLock();
                }
            }
            catch (Exception ex)
            {
                _errorHandler.LogError(ex);
                throw;
            }
        }

        /// <summary>
        /// Sets specified bank from string.
        /// </summary>
        public void SetBank(BankType bank, string value)
        {
            try
            {
                if (bank != BankType.Output)
                    throw new Exception($"Bank not valid");
                if (value.Length != 8)
                    throw new Exception($"Bank value '{value}' incorrect length");
                if (!Regex.IsMatch(value, @"^[0-1]*$"))
                    throw new Exception($"Bank value '{value}' not valid");

                bool[] buffer = new bool[8];
                for (int i = 0; i < 8; i++)
                    buffer[i] = value[i] == '1';

                _lock.EnterWriteLock();
                try
                {
                    Array.Copy(buffer, 0, _outputWrite, 0, 8);
                }
                finally
                {
                    _lock.ExitWriteLock();
                }
            }
            catch (Exception ex)
            {
                _errorHandler.LogError(ex);
                throw;
            }
        }

        #endregion

        #region Conversions

        /// <summary>
        /// Converts address (address + word + bit) to GPIO index.  Returns 0 if no match.
        /// </summary>
        private static ushort BankBitToIndex(BankType bank, ushort bit)
        {
            if (bank == BankType.Input1)
            {
                switch (bit)
                {
                    case 0:
                        return 2;
                    case 1:
                        return 3;
                    case 2:
                        return 4;
                    case 3:
                        return 5;
                    case 4:
                        return 6;
                    case 5:
                        return 7;
                    case 6:
                        return 8;
                    case 7:
                        return 9;
                    default:
                        return 0;
                }
            }
            else if (bank == BankType.Input2)
            {
                switch (bit)
                {
                    case 0:
                        return 20;
                    case 1:
                        return 21;
                    case 2:
                        return 22;
                    case 3:
                        return 23;
                    case 4:
                        return 24;
                    case 5:
                        return 25;
                    case 6:
                        return 26;
                    case 7:
                        return 27;
                    default:
                        return 0;
                }
            }
            else if (bank == BankType.Output)
            {
                switch (bit)
                {
                    case 0:
                        return 10;
                    case 1:
                        return 11;
                    case 2:
                        return 12;
                    case 3:
                        return 13;
                    case 4:
                        return 16;
                    case 5:
                        return 17;
                    case 6:
                        return 18;
                    case 7:
                        return 19;
                    default:
                        return 0;
                }
            }
            return 0;
        }

        /// <summary>
        /// Converts bank to a string of 1's and 0's.
        /// </summary>
        private static string BankToString(bool[] bank)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < bank.Length; i++)
                sb.Append(bank[i] ? "1" : "0");
            return sb.ToString();
        }

        #endregion

        #region Polling Thread

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
                        //cps
                        _cps.Increment();

                        //write data from 'output write' buffer into pin holder
                        for (ushort i = 0; i < 8; i++)
                        {
                            Pin pin = _pins[BankBitToIndex(BankType.Output, i)];
                            if (pin == null)
                                continue;
                            pin.SetValue(_outputWrite[i]);
                        }

                        //read or write to each pin
                        for (int i = 0; i < 32; i++)
                        {
                            if (_pins[i] == null)
                                continue;                            
                            Pin pin = _pins[i];
                            if (pin.Mode == GpioPinDriveMode.Input)
                                pin.SetValue(pin.BasePin.Read(), _config.StickyHighInputMs);
                            else if (pin.Mode == GpioPinDriveMode.Output)
                                pin.BasePin.Write(pin.GetValue());
                        }

                        //copy pin values to 'input 1' buffer
                        for (ushort i = 0; i < 8; i++)
                        {
                            Pin pin = _pins[BankBitToIndex(BankType.Input1, i)];
                            if (pin == null)
                                continue;
                            _input1[i] = pin.GetValue();
                        }

                        //copy pin values to 'input 2' buffer
                        for (ushort i = 0; i < 8; i++)
                        {
                            Pin pin = _pins[BankBitToIndex(BankType.Input2, i)];
                            if (pin == null)
                                continue;
                            _input2[i] = pin.GetValue();
                        }

                        //copy pin values to 'output read' buffer
                        for (ushort i = 0; i < 8; i++)
                        {
                            Pin pin = _pins[BankBitToIndex(BankType.Output, i)];
                            if (pin == null)
                                continue;
                            _outputRead[i] = pin.GetValue();
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
                Thread.Sleep(_config.PollingIntervalMs);
            }
        }

        #endregion

        #region Maintenance

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

        #endregion

        #region Statistics

        /// <summary>
        /// Writes runtime stats.
        /// </summary>
        public void WriteRuntimeStatistics(SimpleJsonWriter writer)
        {
            string input1, input2, output;
            
            _lock.EnterReadLock();
            try
            {
                input1 = BankToString(_input1);
                input2 = BankToString(_input2);
                output = BankToString(_outputRead);
            }
            finally
            {
                _lock.ExitReadLock();
            }

            writer.WriteStartObject("gpio");
            writer.WritePropertyValue("input1", input1);
            writer.WritePropertyValue("input2", input2);
            writer.WritePropertyValue("output", output);
            writer.WritePropertyValue("cps", _cps != null ? _cps.CPS.ToString("0.0") : "0.0");
            writer.WriteEndObject();
        }

        #endregion

        #region Internal Classes

        /// <summary>
        /// Represents a pin and its properties.
        /// </summary>
        private class Pin
        {
            private readonly IGpioPin _basePin;
            private readonly GpioPinDriveMode _mode;
            private readonly BankType _bank;
            private readonly ushort _bit;
            private readonly ushort _gpioID;
            private readonly ushort _pinID;
            private bool _value;
            private DateTime _highUntilTime;

            public IGpioPin BasePin => _basePin;
            public GpioPinDriveMode Mode => _mode;
            public BankType Bank => _bank;
            public ushort Bit => _bit;
            public ushort GpioID => _gpioID;
            public ushort PinID => _pinID;

            public Pin(IGpioPin basePin, GpioPinDriveMode mode, BankType bank, ushort bit, ushort gpioID, ushort pinID)
            {
                _basePin = basePin;
                _mode = mode;
                _bank = bank;
                _bit = bit;
                _gpioID = gpioID;
                _pinID = pinID;
                _value = false;
                _highUntilTime = DateTime.MinValue;
            }

            public void SetValue(bool value, int stickyMs = 0)
            {
                if (_mode == GpioPinDriveMode.Input)
                {
                    if ((value == true) && (_value == false))
                        _highUntilTime = DateTime.Now.AddMilliseconds(stickyMs);
                    else if (value == false)
                        _highUntilTime = DateTime.MinValue;
                }
                _value = value;
            }

            public bool GetValue()
            {
                if (_mode == GpioPinDriveMode.Input)
                {
                    if (DateTime.Now < _highUntilTime)
                        return true;
                }
                return _value;
            }
        }

        #endregion    
    }

    #region Public Classes

    /// <summary>
    /// IO bank designation.
    /// </summary>
    public enum BankType
    {
        Input1,
        Input2,
        Output
    }

    #endregion

}
