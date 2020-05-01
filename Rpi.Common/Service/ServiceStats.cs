using Rpi.Common.Error;
using Rpi.Common.Extensions;
using Rpi.Common.Json;
using Rpi.Common.Output;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Rpi.Common.Service
{
    /// <summary>
    /// Keeps in memory a record of service commands, operations, and tasks.  Rolls this data into presentable summaries.
    /// Commands equal valid HTTP commands sent to the service and are tracked by frequency and average duration.
    /// Operations are short-running internal processes that are reported after the fact, to allow tracking by frequency and average duration.
    /// Tasks are internal processes that are expected to take longer than operations.  We record their begin and end times
    /// to allow monitoring of tasks that are still in operation.
    /// </summary>
    public class ServiceStats : IStatsWriter
    {
        //private
        private readonly IErrorHandler _errorHandler = null;
        private Dictionary<string, List<OperationStats>> _operationQueue = new Dictionary<string, List<OperationStats>>();
        private Dictionary<string, List<OperationRollup>> _operationRollups = new Dictionary<string, List<OperationRollup>>();
        private Dictionary<string, List<OperationStats>> _commandQueue = new Dictionary<string, List<OperationStats>>();
        private Dictionary<string, List<OperationRollup>> _commandRollups = new Dictionary<string, List<OperationRollup>>();
        private readonly Dictionary<Guid, TaskStats> _runningTasks = new Dictionary<Guid, TaskStats>();
        private readonly List<TaskStats> _completedTasks = new List<TaskStats>();
        private DateTime _lastSwap = DateTime.Now;
        private readonly object _queueLock = new object();
        private readonly object _rollupLock = new object();
        private readonly object _taskLock = new object();
        private Thread _processThread = null;
        private readonly ManualResetEventSlim _signal = new ManualResetEventSlim(false);

        /// <summary>
        /// Object constructor.
        /// </summary>
        public ServiceStats(IErrorHandler errorHandler)
        {
            //vars
            _errorHandler = errorHandler;

            //start process thread
            _processThread = new Thread(new ThreadStart(Process_Thread))
            {
                IsBackground = true
            };
            _processThread.Start();
        }

        /// <summary>
        /// Logs an operation, something that happens internally and should be tracked - both by frequency and duration.
        /// </summary>
        public void LogOperation(string name, TimeSpan elapsed)
        {
            LogOperation(name, (Int64)elapsed.TotalMilliseconds);
        }

        /// <summary>
        /// Logs an operation, something that happens internally and should be tracked - both by frequency and duration.
        /// </summary>
        public void LogOperation(string name, long elapsed)
        {
            try
            {
                if (name == null)
                    name = String.Empty;
                var s = new OperationStats(name, elapsed, DateTime.Now);
                lock (_queueLock)
                {
                    if (!_operationQueue.ContainsKey(name))
                        _operationQueue.Add(name, new List<OperationStats>());
                    _operationQueue[name].Add(s);
                }
            }
            catch (Exception ex)
            {
                if (_errorHandler != null)
                    _errorHandler.LogError(ex);
            }
        }

        /// <summary>
        /// Logs a command, something that was sent via HTTP request to the service.
        /// IsStatusPing tells us if the command was just a load balancer ping or other non-work status call.
        /// </summary>
        public void LogCommand(string name, TimeSpan elapsed, bool isStatusPing)
        {
            LogCommand(name, (Int64)elapsed.TotalMilliseconds, isStatusPing);
        }

        /// <summary>
        /// Logs a command, something that was sent via HTTP request to the service.
        /// IsStatusPing tells us if the command was just a load balancer ping or other non-work status call.
        /// </summary>
        public void LogCommand(string name, long elapsed, bool isStatusPing)
        {
            try
            {
                if (name == null)
                    name = String.Empty;
                var s = new OperationStats(name, elapsed, DateTime.Now, isStatusPing);
                lock (_queueLock)
                {
                    if (!_commandQueue.ContainsKey(name))
                        _commandQueue.Add(name, new List<OperationStats>());
                    _commandQueue[name].Add(s);
                }
            }
            catch (Exception ex)
            {
                if (_errorHandler != null)
                    _errorHandler.LogError(ex);
            }
        }

        /// <summary>
        /// Starts a long running task and returns the task's ID.
        /// Task must be ended after completion.
        /// </summary>
        public Guid BeginTask(string name)
        {
            return BeginTask(name, 0);
        }

        /// <summary>
        /// Starts a long running task and returns the task's ID.
        /// Task must be ended after completion.
        /// </summary>
        public Guid BeginTask(string name, long totalIterations)
        {
            try
            {
                var t = new TaskStats(name, totalIterations);
                lock (_taskLock)
                {
                    if (!_runningTasks.ContainsKey(t.ID))
                        _runningTasks.Add(t.ID, t);
                }
                return t.ID;
            }
            catch (Exception ex)
            {
                if (_errorHandler != null)
                    _errorHandler.LogError(ex);
            }
            return Guid.Empty;
        }

        /// <summary>
        /// Sets the total number of iterations after a task has been started, when the total isn't known until later.
        /// </summary>
        public void SetTaskIterations(Guid id, long totalIterations)
        {
            try
            {
                if (id == Guid.Empty)
                    return;

                lock (_taskLock)
                {
                    if (_runningTasks.ContainsKey(id))
                    {
                        TaskStats t = _runningTasks[id];
                        t.SetTotalIterations(totalIterations);
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
        /// Updates running task with new number of completed iterations.
        /// This number can be just the number of new iterations since the last update, or the total number of iterations since the task was started.
        /// </summary>
        public void UpdateTask(Guid id, long completedIterations, bool isTotal)
        {
            try
            {
                if (id == Guid.Empty)
                    return;

                lock (_taskLock)
                {
                    if (_runningTasks.ContainsKey(id))
                    {
                        TaskStats t = _runningTasks[id];
                        t.UpdateTask(completedIterations, isTotal);
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
        /// Ends the specified task.
        /// </summary>
        public void EndTask(Guid id)
        {
            try
            {
                if (id == Guid.Empty)
                    return;

                TaskStats t = null;
                lock (_taskLock)
                {
                    if (_runningTasks.ContainsKey(id))
                    {
                        t = _runningTasks[id];
                        _runningTasks.Remove(id);
                    }

                    if (t != null)
                    {
                        t.EndTask();
                        _completedTasks.Add(t);
                        DateTime now = DateTime.Now;
                        DateTime sixHoursAgo = now.Subtract(TimeSpan.FromHours(6));
                        while ((_completedTasks.Count > 0) && (_completedTasks[0].EndTime < sixHoursAgo))
                            _completedTasks.RemoveAt(0);
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
        /// Internal process thread.
        /// </summary>
        private void Process_Thread()
        {
            try
            {
                _signal.Set();

                while (true)
                {
                    try
                    {
                        DateTime now = DateTime.Now;
                        DateTime sixHoursAgo = now.Subtract(TimeSpan.FromHours(6));

                        if (now >= _lastSwap.AddSeconds(6))
                        {
                            SwapAndRollup(ref _operationQueue, ref _operationRollups, now, sixHoursAgo);
                            SwapAndRollup(ref _commandQueue, ref _commandRollups, now, sixHoursAgo);
                            _lastSwap = now;
                        }
                    }
                    catch (Exception ex)
                    {
                        if (_errorHandler != null)
                            _errorHandler.LogError(ex);
                    }

                    Thread.Sleep(250);
                }
            }
            catch (Exception ex)
            {
                if (_errorHandler != null)
                    _errorHandler.LogError(ex);
            }
        }

        /// <summary>
        /// Rolls up the six second queue into a single object for each unique command or operation.  
        /// Logs each to the AWS CloudWatch service, then appends each to the six hour rolling time window.
        /// Removes items from the six hour window if they have expired.
        /// </summary>
        private void SwapAndRollup(ref Dictionary<string, List<OperationStats>> queue, ref Dictionary<string, List<OperationRollup>> rollups, DateTime now, DateTime sixHoursAgo)
        {
            try
            {
                Dictionary<string, List<OperationStats>> swap;
                lock (_queueLock)
                {
                    swap = queue;
                    queue = new Dictionary<string, List<OperationStats>>();
                }
                var rs = new List<OperationRollup>();
                foreach (List<OperationStats> ss in swap.Values)
                {
                    OperationRollup r = RollupStats(ss, _lastSwap, now);
                    if (r != null)
                        rs.Add(r);
                }
                lock (_rollupLock)
                {
                    foreach (OperationRollup r in rs)
                    {
                        if (!rollups.ContainsKey(r.Name))
                            rollups.Add(r.Name, new List<OperationRollup>());
                        rollups[r.Name].Add(r);
                    }
                    foreach (string name in rollups.Keys)
                    {
                        while ((rollups[name].Count > 0) && (rollups[name][0].StartTime < sixHoursAgo))
                        {
                            rollups[name].RemoveAt(0);
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
        /// Combines a list of stats into a rollup object.
        /// </summary>
        private OperationRollup RollupStats(List<OperationStats> stats, DateTime startTime, DateTime endTime)
        {
            try
            {
                if (stats.Count == 0)
                    return null;

                long count = 0;
                long elapsedSum = 0;
                long elapsedMin = Int64.MaxValue;
                long elapsedMax = Int64.MinValue;

                foreach (OperationStats s in stats)
                {
                    count++;
                    elapsedSum += s.Elapsed;
                    if (s.Elapsed < elapsedMin)
                        elapsedMin = s.Elapsed;
                    if (s.Elapsed > elapsedMax)
                        elapsedMax = s.Elapsed;
                }

                return new OperationRollup(stats[0].Name, count, elapsedSum, elapsedMin, elapsedMax, stats[0].IsPing, startTime, endTime);
            }
            catch (Exception ex)
            {
                if (_errorHandler != null)
                    _errorHandler.LogError(ex);
            }

            return null;
        }

        /// <summary>
        /// Combines a list of rollup objects into a single rollup object.
        /// </summary>
        private OperationRollup RollupRollups(List<OperationRollup> rollups, DateTime endTime)
        {
            try
            {
                if (rollups.Count == 0)
                    return null;

                long count = 0;
                long elapsedSum = 0;
                long elapsedMin = Int64.MaxValue;
                long elapsedMax = Int64.MinValue;
                DateTime startTime = DateTime.MaxValue;

                foreach (OperationRollup r in rollups)
                {
                    count += r.Count;
                    elapsedSum += r.ElapsedSum;
                    if (r.ElapsedMin < elapsedMin)
                        elapsedMin = r.ElapsedMin;
                    if (r.ElapsedMax > elapsedMax)
                        elapsedMax = r.ElapsedMax;
                    if (r.StartTime < startTime)
                        startTime = r.StartTime;
                }

                OperationRollup rx = new OperationRollup(rollups[0].Name, count, elapsedSum, elapsedMin, elapsedMax, rollups[0].IsPing, startTime, endTime);
                return rx;
            }
            catch (Exception ex)
            {
                if (_errorHandler != null)
                    _errorHandler.LogError(ex);
            }

            return null;
        }

        /// <summary>
        /// Called by external maintenance thread.
        /// </summary>
        public void HealthCheck()
        {
            try
            {
                _signal.Wait();
                if (!_processThread.IsAlive)
                {
                    _signal.Reset();
                    _processThread = new Thread(new ThreadStart(Process_Thread))
                    {
                        IsBackground = true
                    };
                    _processThread.Start();
                    throw new Exception("Service stats process thread died and had to be restarted");
                }
            }
            catch (Exception ex)
            {
                if (_errorHandler != null)
                    _errorHandler.LogError(ex);
            }
        }

        /// <summary>
        /// Calculates and returns a summary of each of the stored statistic types.
        /// </summary>
        public void GetSummary(out List<OperationRollup> operations, out List<OperationRollup> commands, out List<TaskStats> runningTasks, out List<TaskStats> completedTasks)
        {
            operations = new List<OperationRollup>();
            commands = new List<OperationRollup>();
            runningTasks = new List<TaskStats>();
            completedTasks = new List<TaskStats>();

            try
            {
                lock (_rollupLock)
                {
                    foreach (string name in _operationRollups.Keys)
                    {
                        OperationRollup r = RollupRollups(_operationRollups[name], _lastSwap);
                        if (r != null)
                            operations.Add(r);
                    }
                    foreach (string name in _commandRollups.Keys)
                    {
                        OperationRollup r = RollupRollups(_commandRollups[name], _lastSwap);
                        if (r != null)
                            commands.Add(r);
                    }
                }
                lock (_taskLock)
                {
                    DateTime now = DateTime.Now;
                    DateTime sixHoursAgo = now.Subtract(TimeSpan.FromHours(6));
                    while ((_completedTasks.Count > 0) && (_completedTasks[0].EndTime < sixHoursAgo))
                        _completedTasks.RemoveAt(0);
                    foreach (Guid id in _runningTasks.Keys)
                    {
                        TaskStats t = _runningTasks[id].Clone();
                        runningTasks.Add(t);
                    }
                    foreach (TaskStats tx in _completedTasks)
                    {
                        TaskStats t = tx.Clone();
                        completedTasks.Add(t);
                    }
                }
                operations = operations.OrderBy(x => x.Name).ToList();
                commands = commands.OrderBy(x => x.Name).ToList();
                runningTasks = runningTasks.OrderBy(x => x.StartTime).ToList();
                completedTasks = completedTasks.OrderBy(x => x.StartTime).ToList();
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
                GetSummary(
                    out List<OperationRollup> operations, 
                    out List<OperationRollup> commands,
                    out List<TaskStats> runningTasks, 
                    out List<TaskStats> completedTasks);

                writer.WriteStartObject("serviceStats");
                writer.WriteStartArray("operations");
                foreach (OperationRollup r in operations)
                {
                    writer.WriteStartObject();
                    writer.WritePropertyValue("name", r.Name ?? String.Empty);
                    writer.WritePropertyValue("count", r.Count);
                    writer.WritePropertyValue("elapsedAvg", Math.Round(r.ElapsedAvg, 1));
                    writer.WritePropertyValue("elapsedMin", r.ElapsedMin);
                    writer.WritePropertyValue("elapsedMax", r.ElapsedMax);
                    writer.WritePropertyValue("elapsedSum", r.ElapsedSum);
                    writer.WritePropertyValue("cps", Math.Round(r.CPS, 2));
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();
                writer.WriteStartArray("commands");
                foreach (OperationRollup r in commands)
                {
                    writer.WriteStartObject();
                    writer.WritePropertyValue("name", r.Name ?? String.Empty);
                    writer.WritePropertyValue("count", r.Count);
                    writer.WritePropertyValue("elapsedAvg", Math.Round(r.ElapsedAvg, 1));
                    writer.WritePropertyValue("elapsedMin", r.ElapsedMin);
                    writer.WritePropertyValue("elapsedMax", r.ElapsedMax);
                    writer.WritePropertyValue("elapsedSum", r.ElapsedSum);
                    writer.WritePropertyValue("cps", Math.Round(r.CPS, 2));
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();
                writer.WriteStartArray("completedTasks");
                foreach (TaskStats t in completedTasks)
                {
                    writer.WriteStartObject();
                    writer.WritePropertyValue("name", t.Name ?? String.Empty);
                    writer.WritePropertyValue("ips", Math.Round(t.IterationsPerSecond, 1));
                    writer.WritePropertyValue("elapsed", t.Elapsed.ToShortString());
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();
                writer.WriteStartArray("runningTasks");
                foreach (TaskStats t in runningTasks)
                {
                    writer.WriteStartObject();
                    writer.WritePropertyValue("name", t.Name ?? String.Empty);
                    writer.WritePropertyValue("ips", Math.Round(t.IterationsPerSecond, 1));
                    writer.WritePropertyValue("percentComplete", Math.Round(t.PercentComplete, 1));
                    writer.WritePropertyValue("eta", t.ETA.ToShortString());
                    writer.WritePropertyValue("elapsed", t.Elapsed.ToShortString());
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();
                writer.WriteEndObject();
            }
            catch (Exception ex)
            {
                if (_errorHandler != null)
                    _errorHandler.LogError(ex);
            }
        }
    }

    /// <summary>
    /// Represents statistics for a long running service task, like the daily loading of a very large data file.
    /// </summary>
    public class TaskStats
    {
        //private
        private readonly Guid _id;
        private readonly string _name;
        private readonly DateTime _startTime;
        private DateTime _endTime;
        private long _totalIterations;
        private long _completedIterations;
        private bool _isComplete;

        //public
        public Guid ID { get { return _id; } }
        public string Name { get { return _name; } }
        public DateTime StartTime { get { return _startTime; } }
        public DateTime EndTime { get { return _endTime; } }
        public long TotalIterations { get { return _totalIterations; } }
        public long CompletedIterations { get { return _completedIterations; } }
        public bool IsComplete { get { return _isComplete; } }
        public TimeSpan Elapsed { get { return (_isComplete ? (TimeSpan)(_endTime - _startTime) : (TimeSpan)(DateTime.Now - _startTime)); } }
        public double PercentComplete { get { return ((_totalIterations > 0) ? ((Double)_completedIterations / (Double)_totalIterations) * 100d : 0); } }
        public double IterationsPerSecond { get { return (Elapsed.TotalSeconds > 0 ? (Double)_completedIterations / Elapsed.TotalSeconds : 0); } }
        public TimeSpan ETA { get { return (((_totalIterations > 0) && (IterationsPerSecond > 0)) ? TimeSpan.FromSeconds((((Double)(_totalIterations - _completedIterations)) / IterationsPerSecond) * 1.25d) : TimeSpan.Zero); } }

        /// <summary>
        /// Class constructor.
        /// </summary>
        public TaskStats(string name)
        {
            _id = Guid.NewGuid();
            _name = name;
            _startTime = DateTime.Now;
            _endTime = DateTime.MinValue;
            _totalIterations = 0;
            _completedIterations = 0;
            _isComplete = false;
        }

        /// <summary>
        /// Class constructor.
        /// </summary>
        public TaskStats(string name, long totalIterations)
        {
            _id = Guid.NewGuid();
            _name = name;
            _startTime = DateTime.Now;
            _endTime = DateTime.MinValue;
            _totalIterations = totalIterations;
            _completedIterations = 0;
            _isComplete = false;
        }

        /// <summary>
        /// Class constructor.
        /// </summary>
        public TaskStats(Guid id, string name, DateTime startTime, DateTime endTime, long totalIterations, long completedIterations, bool isComplete)
        {
            _id = id;
            _name = name;
            _startTime = startTime;
            _endTime = endTime;
            _totalIterations = totalIterations;
            _completedIterations = completedIterations;
            _isComplete = isComplete;
        }

        /// <summary>
        /// Sets the total number of iterations for this task, since sometimes we don't know the number until after the task has begun.
        /// </summary>
        public void SetTotalIterations(long totalIterations)
        {
            _totalIterations = totalIterations;
        }

        /// <summary>
        /// Updates running task with new number of completed iterations.
        /// This number can be just the number of new iterations since the last update, or the total number of iterations since the task was started.
        /// </summary>
        public void UpdateTask(long completedIterations, bool isTotal)
        {
            if (isTotal)
                _completedIterations = completedIterations;
            else
                _completedIterations += completedIterations;
        }

        /// <summary>
        /// Marks the task as complete and records the end time.
        /// </summary>
        public void EndTask()
        {
            _endTime = DateTime.Now;
            _completedIterations = _totalIterations;
            _isComplete = true;
        }

        /// <summary>
        /// Returns a clone of the current object.
        /// </summary>
        public TaskStats Clone()
        {
            TaskStats t = new TaskStats(_id, _name, _startTime, _endTime, _totalIterations, _completedIterations, _isComplete);
            return t;
        }
    }

    /// <summary>
    /// Stores statistics about a single HTTP command the service has processed.
    /// </summary>
    public class OperationStats
    {
        //private
        private readonly string _name;
        private readonly long _elapsed;
        private readonly DateTime _timestamp;
        private readonly bool _isPing;

        //public
        public string Name => _name;
        public long Elapsed => _elapsed;
        public DateTime Timestamp => _timestamp;
        public bool IsPing => _isPing;

        /// <summary>
        /// Class constructor.
        /// </summary>
        public OperationStats(string name, long elapsed, DateTime timestamp)
        {
            _name = name;
            _elapsed = elapsed;
            _timestamp = timestamp;
            _isPing = false;
        }

        /// <summary>
        /// Class constructor.
        /// </summary>
        public OperationStats(string name, long elapsed, DateTime timestamp, bool isPing)
        {
            _name = name;
            _elapsed = elapsed;
            _timestamp = timestamp;
            _isPing = isPing;
        }
    }

    /// <summary>
    /// Stores rollup statistics for multiple of the same command, over a short window of time.
    /// </summary>
    public class OperationRollup
    {
        //private
        private readonly string _name;
        private readonly long _count;
        private readonly long _elapsedSum;
        private readonly long _elapsedMin;
        private readonly long _elapsedMax;
        private readonly bool _isPing;
        private readonly DateTime _startTime;
        private readonly DateTime _endTime;

        //public
        public string Name => _name;
        public long Count => _count;
        public long ElapsedSum => _elapsedSum;
        public long ElapsedMin => _elapsedMin;
        public long ElapsedMax => _elapsedMax;
        public bool IsPing => _isPing;
        public DateTime StartTime => _startTime;
        public DateTime EndTime => _endTime;
        public double ElapsedAvg => (Double)_elapsedSum / (Double)_count;
        public double CPS => (Double)_count / ((TimeSpan)(_endTime - _startTime)).TotalSeconds;
        public double CPM => (Double)_count / ((TimeSpan)(_endTime - _startTime)).TotalMinutes;
        public DateTime MidpointTime => new DateTime(((_endTime.Ticks - _startTime.Ticks) / 2) + _startTime.Ticks);

        /// <summary>
        /// Class constructor.
        /// </summary>
        public OperationRollup(string name, long count, long elapsedSum, long elapsedMin, long elapsedMax, bool isPing, DateTime startTime, DateTime endTime)
        {
            _name = name;
            _count = count;
            _elapsedSum = elapsedSum;
            _elapsedMin = elapsedMin;
            _elapsedMax = elapsedMax;
            _isPing = isPing;
            _startTime = startTime;
            _endTime = endTime;
        }
    }

}
