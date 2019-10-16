using System;
using System.Threading;
using Timer = System.Timers.Timer;

namespace Rpi.Threading
{
    /// <summary>
    /// Wrapper around System.Timers.Timer.  Simple implementation / callback and doesn't conflict 
    /// with System.Threading namespace.  Defaults to auto-start (similar to System.Threading.Timer).  
    /// Defaults to solo mode (built-in Monitor.TryEnter) to allow only one thread/instance of running
    /// callback at a time (instead of implementing this over and over in the external events).
    /// </summary>
    public class SimpleTimer : IDisposable
    {
        //private
        private readonly bool _autoStart;
        private readonly bool _solo;
        private readonly Timer _timer;

        //public
        public bool AutoStart { get { return _autoStart; } }
        public bool Solo { get { return _solo; } }
        public double Interval { get { return _timer.Interval; } set { _timer.Interval = value; } }
        public bool Enabled { get { return _timer.Enabled; } set { _timer.Enabled = value; } }

        //events
        public event Action Elapsed = delegate { };

        /// <summary>
        /// Class constructor.
        /// </summary>
        public SimpleTimer(Action elapsedCallback, TimeSpan interval, bool autoStart = true, bool solo = true, bool autoReset = true)
        {
            Elapsed += elapsedCallback;
            _autoStart = autoStart;
            _solo = solo;
            _timer = new Timer
            {
                Interval = interval.TotalMilliseconds,
                AutoReset = autoReset
            };
            _timer.Elapsed += Timer_Elapsed;
            if (autoStart)
                _timer.Start();
        }

        /// <summary>
        /// Class constructor.
        /// </summary>
        public SimpleTimer(Action elapsedCallback, double interval, bool autoStart = true, bool solo = true, bool autoReset = true)
        {
            Elapsed += elapsedCallback;
            _autoStart = autoStart;
            _solo = solo;
            _timer = new Timer
            {
                Interval = interval,
                AutoReset = autoReset
            };
            _timer.Elapsed += Timer_Elapsed;
            if (autoStart)
                _timer.Start();
        }

        /// <summary>
        /// Starts the timer by setting Timer.Enabled to true.
        /// </summary>
        public void Start()
        {
            _timer.Start();
        }

        /// <summary>
        /// Stops the timer by setting Timer.Enabled to true.
        /// </summary>
        public void Stop()
        {
            _timer.Stop();
        }

        /// <summary>
        /// Fired by internal timer, raises external event.
        /// </summary>
        private void Timer_Elapsed(object sender, EventArgs e)
        {
            if (_solo)
            {
                if (Monitor.TryEnter(this))
                {
                    try
                    {
                        Elapsed?.Invoke();
                    }
                    finally
                    {
                        Monitor.Exit(this);
                    }
                }
            }
            else
            {
                Elapsed?.Invoke();
            }
        }

        /// <summary>
        /// Dispose of resources.
        /// </summary>
        public void Dispose()
        {
            _timer.Dispose();
        }
    }
}
