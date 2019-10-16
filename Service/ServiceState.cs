using Rpi.Error;
using Rpi.Json;
using Rpi.Output;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Rpi.Service
{
    public class ServiceState : IStatsWriter
    {
        //private
        private readonly IErrorHandler _errorHandler = null;
        private ServiceStateType _state = ServiceStateType.Down;
        private List<Alert> _alerts = new List<Alert>();
        private readonly object _lock = new object();
        private readonly Timer _timer = null;

        /// <summary>
        /// Object constructor.
        /// </summary>
        public ServiceState(IErrorHandler errorHandler, ServiceStateType initialState = ServiceStateType.Down)
        {
            Log.WriteMessage("ServiceState", "Creating service state..");
            _errorHandler = errorHandler;
            _state = initialState;
            _timer = new Timer(Timer_Callback, null, TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(60));
        }

        /// <summary>
        /// Adds a warning alert to the system with the specified expiration.
        /// </summary>
        public void AddWarningAlert(string message, TimeSpan expiry)
        {
            try
            {
                Log.WriteMessage("ServiceState", $"Adding warning alert: {message}");
                lock (_lock)
                {
                    foreach (Alert a in _alerts)
                    {
                        if (String.Compare(a.Message, message) == 0)
                        {
                            a.UpdateExpiration(expiry);
                            return;
                        }
                    }

                    if (_alerts.Count >= 10)
                        return;

                    _alerts.Add(new Alert(ServiceHealthType.Warning, message, expiry));
                }
            }
            catch (Exception ex)
            {
                if (_errorHandler != null)
                    _errorHandler.LogError(ex);
            }
        }

        /// <summary>
        /// Adds a critical alert to the system with the specified expiration.
        /// </summary>
        public void AddCriticalAlert(string message, TimeSpan expiry)
        {
            try
            {
                Log.WriteMessage("ServiceState", $"Adding critical alert: {message}");
                lock (_lock)
                {
                    foreach (Alert a in _alerts)
                    {
                        if (String.Compare(a.Message, message) == 0)
                        {
                            a.UpdateExpiration(expiry);
                            return;
                        }
                    }

                    if (_alerts.Count >= 10)
                    {
                        for (int i = 0; i < _alerts.Count; i++)
                        {
                            if ((_alerts[i].Type == ServiceHealthType.Warning) && (_alerts[i].Expiration != DateTime.MaxValue))
                            {
                                _alerts.RemoveAt(i);
                                break;
                            }
                        }

                        if (_alerts.Count >= 10)
                            return;
                    }

                    _alerts.Add(new Alert(ServiceHealthType.Critical, message, expiry));
                }
            }
            catch (Exception ex)
            {
                if (_errorHandler != null)
                    _errorHandler.LogError(ex);
            }
        }

        /// <summary>
        /// Sets the state of the service.
        /// </summary>
        public void SetState(ServiceStateType state)
        {
            try
            {
                Log.WriteMessage("ServiceState", $"Setting state: {state}");
                lock (_lock)
                {
                    _state = state;
                }
            }
            catch (Exception ex)
            {
                if (_errorHandler != null)
                    _errorHandler.LogError(ex);
            }
        }

        /// <summary>
        /// Gets the state of the service.
        /// </summary>
        public ServiceStateType GetState()
        {
            var state = ServiceStateType.Up;

            try
            {
                lock (_lock)
                {
                    state = _state;
                }
            }
            catch (Exception ex)
            {
                if (_errorHandler != null)
                    _errorHandler.LogError(ex);
            }

            return state;
        }

        /// <summary>
        /// Goes through list of alerts, removes ones that have expired.
        /// </summary>
        private void RemoveExpiredAlerts()
        {
            try
            {
                var now = DateTime.Now;
                lock (_lock)
                {
                    bool changes = false;
                    foreach (Alert a in _alerts)
                    {
                        if (a.Expiration >= now)
                        {
                            changes = true;
                            break;
                        }
                    }
                    if (changes)
                    {
                        List<Alert> alerts = new List<Alert>();
                        foreach (Alert a in _alerts)
                        {
                            if (a.Expiration < now)
                                alerts.Add(a);
                        }
                        _alerts = alerts;
                    }
                }
            }
            catch (Exception ex)
            {
                if (_errorHandler != null)
                    _errorHandler.LogError(ex);
            }
        }

        /// <summary>
        /// Timer callback.
        /// </summary>
        private void Timer_Callback(object state)
        {
            try
            {
                RemoveExpiredAlerts();
            }
            catch (Exception ex)
            {
                if (_errorHandler != null)
                    _errorHandler.LogError(ex);
            }
        }

        /// <summary>
        /// Writes runtime statistics.
        /// </summary>
        public void WriteRuntimeStatistics(SimpleJsonWriter writer)
        {
            try
            {
                var now = DateTime.Now;
                var state = ServiceStateType.Up;
                var health = ServiceHealthType.Good;
                List<Alert> alerts = null;
                lock (_lock)
                {
                    state = _state;
                    foreach (Alert a in _alerts)
                    {
                        if (a.Type > health)
                            health = a.Type;
                    }
                    if (health > ServiceHealthType.Good)
                    {
                        alerts = new List<Alert>();
                        foreach (Alert a in _alerts)
                            alerts.Add(a);
                    }
                }

                writer.WriteStartObject("serviceState");
                writer.WritePropertyValue("state", ((Int32)state) + "-" + state.ToString());
                writer.WritePropertyValue("health", ((Int32)health) + "-" + health.ToString());
                if (alerts != null)
                {
                    writer.WriteStartArray("alerts");
                    foreach (Alert a in alerts)
                    {
                        writer.WriteStartObject();
                        writer.WritePropertyValue("type", ((Int32)a.Type) + "-" + a.Type.ToString());
                        writer.WritePropertyValue("message", a.Message ?? "");
                        writer.WritePropertyValue("time", a.Time.ToString());
                        writer.WritePropertyValue("expiration", a.Expiration == DateTime.MaxValue ? "max" : ((Int32)(((TimeSpan)(a.Expiration - now)).TotalMinutes)).ToString());
                        writer.WriteEndObject();
                    }
                    writer.WriteEndArray();
                }
                writer.WriteEndObject();
            }
            catch (Exception ex)
            {
                if (_errorHandler != null)
                    _errorHandler.LogError(ex);
            }
        }

        /// <summary>
        /// Stores a single alert state, either warning or critical.
        /// Every alert needs an expiration time, though this can be infinate.
        /// </summary>
        private class Alert
        {
            //private
            private readonly ServiceHealthType _type;
            private readonly string _message;
            private readonly DateTime _time;
            private DateTime _expiration;

            //public
            public ServiceHealthType Type { get { return _type; } }
            public string Message { get { return _message; } }
            public DateTime Time { get { return _time; } }
            public DateTime Expiration { get { return _expiration; } }

            //constructor
            public Alert(ServiceHealthType type, string message, TimeSpan expiry)
            {
                _type = type;
                _message = message;
                _time = DateTime.Now;
                _expiration = expiry != TimeSpan.MaxValue ? DateTime.Now.Add(expiry) : DateTime.MaxValue;
            }

            /// <summary>
            /// Updates the expiry of an existing alert.
            /// </summary>
            public void UpdateExpiration(TimeSpan expiry)
            {
                _expiration = expiry != TimeSpan.MaxValue ? DateTime.Now.Add(expiry) : DateTime.MaxValue;
            }
        }
    }

    /// <summary>
    /// Represents the different states a service can be in.
    /// </summary>
    public enum ServiceHealthType
    {
        Good = 0,
        Warning = 1,
        Critical = 2
    }

    /// <summary>
    /// Represents the different states the service can be in, primarily for the 'ping' command.
    /// </summary>
    public enum ServiceStateType
    {
        Up = 0,
        LoadingData = 1,
        Down = 2
    }
}
