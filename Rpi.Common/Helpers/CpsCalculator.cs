using Rpi.Common.Threading;
using System;
using System.Collections.Generic;

namespace Rpi.Common.Helpers
{
    /// <summary>
    /// Calculates count per second for various things, example frames per second.
    /// Uses a sliding window of X seconds.
    /// </summary>
    public class CpsCalculator
    {
        //private
        private readonly TimeSpan _windowTimeSpan;
        private readonly List<Period> _window;
        private readonly SimpleTimer _timer;
        private double _cps;

        /// <summary>
        /// Returns last calculated count per second.
        /// </summary>
        public double CPS
        {
            get
            {
                lock (this)
                {
                    return _cps;
                }
            }
        }

        /// <summary>
        /// Increments counter by one, or by specified value.
        /// </summary>
        public void Increment(long count = 1)
        {
            lock (this)
            {
                _window[_window.Count - 1].Increment(count);
            }
        }

        /// <summary>
        /// Class constructor.
        /// </summary>
        public CpsCalculator(ushort windowSeconds)
        {
            _windowTimeSpan = TimeSpan.FromSeconds(windowSeconds);
            _window = new List<Period>
            {
                new Period()
            };
            _timer = new SimpleTimer(Timer_Callback, 100);
        }

        /// <summary>
        /// Class constructor.
        /// </summary>
        public CpsCalculator(TimeSpan windowTimeSpan)
        {
            _windowTimeSpan = windowTimeSpan;
            _window = new List<Period>
            {
                new Period()
            };
            _timer = new SimpleTimer(Timer_Callback, 100);
        }

        /// <summary>
        /// Fired by timer every 100 ms.
        /// </summary>
        private void Timer_Callback()
        {
            DateTime now = DateTime.Now;
            DateTime outOfRange = now.Subtract(_windowTimeSpan);
            lock (this)
            {
                while ((_window.Count > 0) && (_window[0].Start < outOfRange))
                    _window.RemoveAt(0);
                _window.Add(new Period());
                TimeSpan span = now - _window[0].Start;
                long total = 0;
                foreach (Period s in _window)
                    total += s.Count;
                _cps = span.TotalSeconds > 0d ? total / span.TotalSeconds : 0d;
            }
        }

        /// <summary>
        /// Stores count for a single time period.
        /// </summary>
        private class Period
        {
            public DateTime Start { get; }
            public long Count { get; private set; }

            public Period()
            {
                Start = DateTime.Now;
                Count = 0;
            }

            public void Increment(long count = 1)
            {
                Count += count;
            }
        }

    }
}
