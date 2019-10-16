using Rpi.Json;
using Rpi.Output;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Rpi.Error
{
    /// <summary>
    /// Keeps track of errors thrown by the service, and remembers them for the specified retention interval.
    /// Generates an error report shown in the service's 'getstats' command.
    /// </summary>
    public class ErrorCache : IErrorLogger, IStatsWriter
    {
        //private
        private readonly IErrorHandler _errorHandler = null;
        private readonly TimeSpan _retention = TimeSpan.FromMinutes(60);
        private readonly Queue<SmallError> _errorCache = new Queue<SmallError>();
        private readonly Dictionary<int, Error> _errorDefs = new Dictionary<int, Error>();
        private readonly object _lock = new object();
        private readonly Timer _timer = null;
        private DateTime _startTime = DateTime.Now;

        //public
        public TimeSpan CacheAge { get { return DateTime.Now - _startTime; } }

        /// <summary>
        /// Class constructor.
        /// </summary>
        public ErrorCache(IErrorHandler errorHandler, TimeSpan retention)
        {
            Log.WriteMessage("ErrorCache", "Creating error cache..");
            _errorHandler = errorHandler;
            _retention = retention;
            _timer = new Timer(Timer_Callback, null, TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(60));
        }

        /// <summary>
        /// Add error to cache.
        /// </summary>
        public void LogError(Exception ex)
        {
            LogErrorRecursive(ex);
        }

        /// <summary>
        /// Recursively log error.
        /// </summary>
        private void LogErrorRecursive(Exception ex)
        {
            try
            {
                //create error objects
                var error = new Error(ex);
                var smallError = new SmallError(error.Hash);

                //lock
                lock (_lock)
                {
                    //remove expired items
                    DateTime now = DateTime.Now;
                    DateTime xMinAgo = now.Subtract(_retention);
                    while ((_errorCache.Count > 0) && (_errorCache.Peek().Time < xMinAgo))
                    {
                        _errorCache.Dequeue();
                        if (_startTime != xMinAgo)
                            _startTime = xMinAgo;
                    }

                    //add new item
                    _errorCache.Enqueue(smallError);

                    //add definition, if doesn't exist
                    if (!_errorDefs.ContainsKey(error.Hash))
                        _errorDefs.Add(error.Hash, error);
                }

                //recursively log inner exceptions
                if (ex.InnerException != null)
                    LogErrorRecursive(ex.InnerException);
            }
            catch
            {
            }
        }

        /// <summary>
        /// Generates error report.
        /// </summary>
        public List<ErrorCacheItem> GetErrorReport()
        {
            var report = new List<ErrorCacheItem>();

            try
            {
                lock (_lock)
                {
                    //remove expired items
                    DateTime now = DateTime.Now;
                    DateTime xMinAgo = now.Subtract(_retention);
                    while ((_errorCache.Count > 0) && (_errorCache.Peek().Time < xMinAgo))
                    {
                        _errorCache.Dequeue();
                        if (_startTime != xMinAgo)
                            _startTime = xMinAgo;
                    }
                }

                //temp copy of cache
                Queue<SmallError> tempCache = null;

                lock (_lock)
                {
                    //copy the cache
                    tempCache = new Queue<SmallError>(_errorCache);
                }

                //make sure we have copy
                if (tempCache != null)
                {
                    //iterate through temp cache, build error report
                    var reportDict = new Dictionary<int, ErrorCacheItem>();
                    foreach (SmallError smallError in tempCache)
                    {
                        //doesn't exist yet?
                        if (!reportDict.ContainsKey(smallError.Hash))
                        {
                            //get matching error definition
                            Error error = null;
                            lock (_lock)
                            {
                                if (_errorDefs.ContainsKey(smallError.Hash))
                                    error = _errorDefs[smallError.Hash];
                            }

                            //have matching def?
                            if (error != null)
                            {
                                //add new error
                                var reportItem = new ErrorCacheItem(error, 1);
                                reportDict.Add(smallError.Hash, reportItem);
                            }
                        }
                        else
                        {
                            //increment existing error
                            reportDict[smallError.Hash].Count++;
                        }
                    }

                    //loop through each unique error, calculate errors-per-minute from actual count, save to final report
                    DateTime endTime = DateTime.Now;
                    double minsInCache = ((TimeSpan)(endTime - _startTime)).TotalMinutes;
                    foreach (ErrorCacheItem reportItem in reportDict.Values)
                    {
                        reportItem.CountPerMinute = (double)reportItem.Count / minsInCache;
                        report.Add(reportItem);
                    }

                    //sort report list (desc)
                    report = report.OrderByDescending(e => e.CountPerMinute).ToList();
                }
            }
            catch (Exception ex)
            {
                if (_errorHandler != null)
                    _errorHandler.LogError(ex);
            }

            return report;
        }

        /// <summary>
        /// Writes runtime statistics.
        /// </summary>
        public void WriteRuntimeStatistics(SimpleJsonWriter writer)
        {
            try
            {
                List<ErrorCacheItem> errorStats = GetErrorReport();

                writer.WriteStartArray("errorStats");
                foreach (ErrorCacheItem errorItem in errorStats)
                {
                    writer.WriteStartObject();
                    writer.WritePropertyValue("count", errorItem.Count);
                    writer.WritePropertyValue("cpm", Math.Round(errorItem.CountPerMinute, 1));
                    writer.WritePropertyValue("type", errorItem.Error.Type ?? "");
                    writer.WritePropertyValue("message", errorItem.Error.Message ?? "");
                    writer.WritePropertyValue("stackTrace", errorItem.Error.StackTrace ?? "");
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();
            }
            catch (Exception ex)
            {
                if (_errorHandler != null)
                    _errorHandler.LogError(ex);
            }
        }

        /// <summary>
        /// Timer callback, used to purge unreferenced error definitions from memory.
        /// </summary>
        private void Timer_Callback(object state)
        {
            try
            {
                var usedHashes = new List<int>();
                lock (_lock)
                {
                    foreach (SmallError smallError in _errorCache)
                    {
                        if (!usedHashes.Contains(smallError.Hash))
                            usedHashes.Add(smallError.Hash);
                    }
                }

                var unusedHashes = new List<int>();
                lock (_lock)
                {
                    foreach (int hash in _errorDefs.Keys)
                    {
                        if (!usedHashes.Contains(hash))
                            unusedHashes.Add(hash);
                    }
                }

                if (unusedHashes.Count > 0)
                {
                    lock (_lock)
                    {
                        foreach (int hash in unusedHashes)
                        {
                            _errorDefs.Remove(hash);
                        }
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
        /// Represents a smaller version of the Error class, referencing the Error's hash code.
        /// </summary>
        private class SmallError
        {
            public DateTime Time { get; }
            public int Hash { get; }

            public SmallError(int hash)
            {
                Time = DateTime.Now;
                Hash = hash;
            }
        }



    }

    /// <summary>
    /// Represents select values parsed from Exception object.
    /// </summary>
    public class Error
    {
        public string Type { get; }
        public string Message { get; }
        public string StackTrace { get; }
        public int Hash { get; }

        public Error(Exception ex)
        {
            Type = ex.GetType() != null ? ex.GetType().ToString() : "";
            Message = ex.Message ?? "";
            StackTrace = ex.StackTrace ?? "";
            Hash = (Type + Message + StackTrace).GetHashCode();
        }

        public Error(string type, string message, string stackTrace)
        {
            Type = type ?? "";
            Message = message ?? "";
            StackTrace = stackTrace ?? "";
            Hash = (Type + Message + StackTrace).GetHashCode();
        }
    }

    /// <summary>
    /// Represents one unique error, and its count/count-per-minute, for the configurable time period.
    /// </summary>
    public class ErrorCacheItem
    {
        public int Count { get; set; }
        public double CountPerMinute { get; set; }
        public Error Error { get; }

        public ErrorCacheItem(Error error, int count)
        {
            Error = error;
            Count = count;
        }
    }
}
