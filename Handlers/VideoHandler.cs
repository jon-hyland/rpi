//using Rpi.Error;
//using Rpi.Extensions;
//using Rpi.Http;
//using Rpi.Json;
//using Rpi.Output;
//using Rpi.Service;
//using Rpi.Threading;
//using System;
//using System.Collections.Generic;
//using System.Diagnostics;
//using System.Linq;
//using System.Text;
//using System.Text.RegularExpressions;
//using System.Threading.Tasks;

//namespace Rpi.Handlers
//{
//    /// <summary>
//    /// Handles requests for video commands.
//    /// </summary>
//    public class VideoHandler : HandlerBase, IStatsWriter
//    {
//        //const
//        private const int RTSP_PORT_START = 8501;
//        private const int RTSP_PORT_END = 8599;
//        private readonly HashSet<string> SUPPORTED_VIDEO_DEVICES = new HashSet<string>(new string[] { "nvidia",
//            "video0", "video1", "video2", "video3", "video4", "video5", "video6", "video7", "video8", "video9",
//            "video100", "video101", "video102", "video103", "video104", "video105", "video106", "video107", "video108", "video109",
//            "video200", "video201", "video202", "video203", "video204", "video205", "video206", "video207", "video208", "video209" });
//        private const int GSTD_COMMAND_TIMEOUT = 7500;

//        //private
//        private readonly Dictionary<string, VideoStream> _streams = new Dictionary<string, VideoStream>();
//        private readonly SimpleTimer _refreshTimer = null;

//        /// <summary>
//        /// Class constructor.
//        /// </summary>
//        public VideoHandler(IErrorHandler errorHandler, Config config, ServiceStats serviceStats)
//            : base(errorHandler, config, serviceStats)
//        {
//            _refreshTimer = new SimpleTimer(RefreshTimer_Callback, TimeSpan.FromSeconds(10));
//        }

//        /// <summary>
//        /// Called before HTTP listener started.
//        /// </summary>
//        public override void PreInitialize()
//        {
//            try
//            {
//                int count = __DeleteAllStreams();
//            }
//            catch (Exception ex)
//            {
//                _errorHandler?.LogError(ex);
//            }
//        }

//        /// <summary>
//        /// Called after HTTP listener started.
//        /// </summary>
//        public override void PostInitialize()
//        {
//        }

//        /// <summary>
//        /// Handles HTTP request.
//        /// </summary>
//        protected override async Task HandleRequest(SimpleHttpContext context)
//        {
//            string json;
//            switch (context.Command)
//            {
//                case "liststreams":
//                    json = ListStreams(context);
//                    await context.WriteJson(json);
//                    break;

//                case "registerstream":
//                    json = RegisterStream(context);
//                    await context.WriteJson(json);
//                    break;

//                case "startstream":
//                    json = StartStream(context);
//                    await context.WriteJson(json);
//                    break;

//                case "stopstream":
//                    json = StopStream(context);
//                    await context.WriteJson(json);
//                    break;

//                case "pausestream":
//                    json = PauseStream(context);
//                    await context.WriteJson(json);
//                    break;

//                case "deletestream":
//                    json = DeleteStream(context);
//                    await context.WriteJson(json);
//                    break;

//                default:
//                    string message = "Command not found";
//                    await context.WriteError(message, 404);
//                    throw new Exception(message);
//            }
//        }

//        #region Commands

//        /// <summary>
//        /// Lists all running streams (list_pipelines).
//        /// </summary>
//        private string ListStreams(SimpleHttpContext context)
//        {
//            try
//            {
//                //message
//                Log.WriteMessage("Video", "Executing service command '/video/liststreams'");

//                //get streams
//                List<VideoStream> streams = __ListStreams(out VideoResult result, true);

//                //build response
//                StringBuilder json = new StringBuilder();
//                using (SimpleJsonWriter writer = new SimpleJsonWriter(json))
//                {
//                    writer.WriteStartObject();
//                    WriteServiceObject(writer);
//                    WriteDeviceObject(writer);
//                    WriteRequestObject(writer, context);
//                    writer.WriteStartObject("output");
//                    writer.WritePropertyValue("success", result.Success ? 1 : 0);
//                    writer.WritePropertyValue("code", result.Code);
//                    writer.WritePropertyValue("message", result.Message);
//                    writer.WriteStartArray("streams");
//                    foreach (VideoStream stream in streams)
//                    {
//                        writer.WriteStartObject();
//                        writer.WritePropertyValue("name", stream.Name);
//                        writer.WritePropertyValue("device", stream.Device);
//                        writer.WritePropertyValue("codec", stream.Codec);
//                        writer.WritePropertyValue("width", stream.Width);
//                        writer.WritePropertyValue("height", stream.Height);
//                        writer.WritePropertyValue("flip", stream.Flip);
//                        writer.WritePropertyValue("quality", stream.Quality);
//                        writer.WritePropertyValue("ipAddress", stream.IPAddress);
//                        writer.WritePropertyValue("port", stream.Port);
//                        writer.WritePropertyValue("state", stream.State);
//                        writer.WriteEndObject();
//                    }
//                    writer.WriteEndArray();
//                    writer.WriteEndObject();
//                    writer.WriteEndObject();
//                }
//                return json.ToString();
//            }
//            catch (Exception ex)
//            {
//                _errorHandler?.LogError(ex);
//                return WriteFatalResponse(context, ex);
//            }
//        }

//        /// <summary>
//        /// Lists all running streams (list_pipelines).
//        /// </summary>
//        private List<VideoStream> __ListStreams(out VideoResult result, bool writeLog)
//        {
//            Stopwatch sw = Stopwatch.StartNew();
//            try
//            {
//                //return if windows
//                if (_config.IsWindows)
//                {
//                    result = new VideoResult(1, "No Windows OS support");
//                    return new List<VideoStream>();
//                }

//                //execute
//                string command = "gstd-client list_pipelines";
//                string resp = command.Bash(GSTD_COMMAND_TIMEOUT, writeLog);
//                if (String.IsNullOrWhiteSpace(resp))
//                    throw new Exception("No response from GSTD.. service may not be running");

//                //parse response
//                dynamic data = JsonSerialization.Deserialize(resp);
//                int code = data.code != null ? (int)data.code : 1;
//                string description = data.description != null ? (string)data.description : "";
//                List<string> daemonStreams = new List<string>();
//                if ((data.response != null) && (data.response.nodes != null))
//                {
//                    foreach (dynamic node in data.response.nodes)
//                    {
//                        string name = node.name != null ? (string)node.name : null;
//                        if (name != null)
//                            daemonStreams.Add(name);
//                    }
//                }

//                //get states
//                Dictionary<string, string> states = new Dictionary<string, string>();
//                foreach (string name in daemonStreams)
//                {
//                    //execute
//                    command = $"gstd-client read /pipelines/{name}/state";
//                    resp = command.Bash(GSTD_COMMAND_TIMEOUT, writeLog);
//                    if (String.IsNullOrWhiteSpace(resp))
//                        throw new Exception("No response from GSTD.. service may not be running");

//                    //parse response
//                    data = JsonSerialization.Deserialize(resp);
//                    string state = ((data.response != null) && (data.response.value != null)) ? (string)data.response.value : null;
//                    state = StateToState(state);
//                    if (!states.ContainsKey(name))
//                        states.Add(name, state);
//                }

//                //consolidate and build stream list
//                List<VideoStream> streams;
//                lock (_streams)
//                {
//                    _streams.Keys
//                        .Where(n => !daemonStreams.Contains(n))
//                        .ToList()
//                        .ForEach(n => _streams.Remove(n));

//                    daemonStreams
//                        .Where(n => !_streams.ContainsKey(n))
//                        .ToList()
//                        .ForEach(n =>
//                        {
//                            __DeleteStream(n, out VideoResult resultX);   
//                            if (resultX.Code == 0)
//                                daemonStreams.Remove(n);
//                        });

//                    foreach (string name in _streams.Keys)
//                        _streams[name].State = states.ContainsKey(name) ? states[name] : "unknown";

//                    streams = _streams.Values.OrderBy(s => s.Name).ToList();
//                }

//                //return
//                result = new VideoResult(code, description);
//                return streams;
//            }
//            catch (Exception ex)
//            {
//                _errorHandler?.LogError(ex);
//                result = new VideoResult(1, ex.Message);
//                return new List<VideoStream>();
//            }
//            finally
//            {
//                _serviceStats.LogOperation("VideoHandler.__ListStreams", sw.Elapsed);
//            }
//        }

//        /// <summary>
//        /// Registers a video stream (pipeline_create).
//        /// </summary>
//        private string RegisterStream(SimpleHttpContext context)
//        {
//            try
//            {
//                //message
//                Log.WriteMessage("Video", "Executing service command '/video/registerstream'");

//                //vars
//                string name = context.Query.Get("name") ?? "";
//                string device = context.Query.Get("device") ?? "";
//                string codec = context.Query.Get("codec") ?? "mjpeg";
//                int width = Int32.Parse(context.Query.Get("width") ?? "1280");
//                int height = Int32.Parse(context.Query.Get("height") ?? "720");
//                int flip = Int32.Parse(context.Query.Get("flip") ?? "0");
//                string quality = context.Query.Get("quality") ?? "medium";

//                //register stream
//                VideoStream stream = __RegisterStream(name, device, codec, width, height, flip, quality, out VideoResult result);

//                //build response
//                StringBuilder json = new StringBuilder();
//                using (SimpleJsonWriter writer = new SimpleJsonWriter(json))
//                {
//                    writer.WriteStartObject();
//                    WriteServiceObject(writer);
//                    WriteDeviceObject(writer);
//                    WriteRequestObject(writer, context);
//                    writer.WriteStartObject("output");
//                    writer.WritePropertyValue("success", result.Success ? 1 : 0);
//                    writer.WritePropertyValue("code", result.Code);
//                    writer.WritePropertyValue("message", result.Message);
//                    if (stream != null)
//                    {
//                        writer.WriteStartObject("stream");
//                        writer.WritePropertyValue("name", stream.Name);
//                        writer.WritePropertyValue("device", stream.Device);
//                        writer.WritePropertyValue("codec", stream.Codec);
//                        writer.WritePropertyValue("width", stream.Width);
//                        writer.WritePropertyValue("height", stream.Height);
//                        writer.WritePropertyValue("flip", stream.Flip);
//                        writer.WritePropertyValue("quality", stream.Quality);
//                        writer.WritePropertyValue("ipAddress", stream.IPAddress);
//                        writer.WritePropertyValue("port", stream.Port);
//                        writer.WritePropertyValue("state", stream.State);
//                        writer.WriteEndObject();
//                    }
//                    writer.WriteEndObject();
//                    writer.WriteEndObject();
//                }
//                return json.ToString();
//            }
//            catch (Exception ex)
//            {
//                _errorHandler?.LogError(ex);
//                return WriteFatalResponse(context, ex);
//            }
//        }

//        /// <summary>
//        /// Registers a video stream (pipeline_create).
//        /// </summary>
//        private VideoStream __RegisterStream(string name, string device, string codec, int width, int height, int flip, string quality, out VideoResult result)
//        {
//            Stopwatch sw = Stopwatch.StartNew();
//            try
//            {
//                //return if windows
//                if (_config.IsWindows)
//                {
//                    result = new VideoResult(1, "No Windows OS support");
//                    return null;
//                }

//                //validate name
//                if ((String.IsNullOrWhiteSpace(name)) || (!Regex.IsMatch(name, "^[a-zA-Z][a-zA-Z0-9]*$")))
//                    throw new Exception("Stream name is invalid");

//                //refresh stream list
//                List<VideoStream> streams = __ListStreams(out result, true);

//                //device in use?
//                if (streams.Where(s => (s.Device == device) && (s.Name != name)).Any())
//                    throw new Exception("Device already in use");

//                //same name already exists?
//                if (streams.Where(s => s.Name == name).Any())
//                {
//                    //delete stream
//                    __DeleteStream(name, out result);
//                }

//                //get next port
//                int port = GetNextPort();

//                //build command
//                string command = GstdCommand_PipelineCreate(name, device, codec, width, height, flip, quality, port);

//                //run command
//                string resp = command.Bash(GSTD_COMMAND_TIMEOUT, true);

//                //parse response
//                dynamic data = JsonSerialization.Deserialize(resp);
//                int code = data.code != null ? (int)data.code : 1;
//                string description = data.description != null ? (string)data.description : "";                
//                string state = "stopped";

//                //get properties
//                VideoStream props = null;
//                if (code == 0)
//                {
//                    props = new VideoStream(name, device, codec, width, height, flip, quality, _config.PrimaryInterface.InternetAddress, port, state);
//                    lock (_streams)
//                    {
//                        if (!_streams.ContainsKey(name))
//                            _streams.Add(name, props);
//                        else
//                            _streams[name] = props;
//                    }                        
//                }

//                //return
//                result = new VideoResult(code, description);
//                return props;
//            }
//            catch (Exception ex)
//            {
//                _errorHandler?.LogError(ex);
//                result = new VideoResult(1, ex.Message);
//                return null;
//            }
//            finally
//            {
//                _serviceStats.LogOperation("VideoHandler.__RegisterStream", sw.Elapsed);
//            }
//        }

//        /// <summary>
//        /// Starts a video stream (pipeline_play).
//        /// </summary>
//        private string StartStream(SimpleHttpContext context)
//        {
//            try
//            {
//                //message
//                Log.WriteMessage("Video", "Executing service command '/video/startstream'");

//                //vars
//                string name = context.Query.Get("name") ?? "";

//                //start stream
//                __StartStream(name, out VideoResult result);

//                //build response
//                StringBuilder json = new StringBuilder();
//                using (SimpleJsonWriter writer = new SimpleJsonWriter(json))
//                {
//                    writer.WriteStartObject();
//                    WriteServiceObject(writer);
//                    WriteDeviceObject(writer);
//                    WriteRequestObject(writer, context);
//                    writer.WriteStartObject("output");
//                    writer.WritePropertyValue("success", result.Success ? 1 : 0);
//                    writer.WritePropertyValue("code", result.Code);
//                    writer.WritePropertyValue("message", result.Message);
//                    writer.WriteStartObject("stream");
//                    writer.WritePropertyValue("name", name);
//                    writer.WriteEndObject();
//                    writer.WriteEndObject();
//                    writer.WriteEndObject();
//                }
//                return json.ToString();
//            }
//            catch (Exception ex)
//            {
//                _errorHandler?.LogError(ex);
//                return WriteFatalResponse(context, ex);
//            }
//        }

//        /// <summary>
//        /// Starts a video stream (pipeline_play).
//        /// </summary>
//        private void __StartStream(string name, out VideoResult result)
//        {
//            Stopwatch sw = Stopwatch.StartNew();
//            try
//            {
//                //return if windows
//                if (_config.IsWindows)
//                {
//                    result = new VideoResult(1, "No Windows OS support");
//                    return;
//                }

//                //validate name
//                if ((String.IsNullOrWhiteSpace(name)) || (!Regex.IsMatch(name, "^[a-zA-Z][a-zA-Z0-9]*$")))
//                    throw new Exception("Stream name is invalid");
//                lock (_streams)
//                {
//                    if (!_streams.ContainsKey(name))
//                        throw new Exception("Stream name is not registered");
//                }

//                //execute
//                string command = $"gstd-client pipeline_play {name}";
//                string resp = command.Bash(GSTD_COMMAND_TIMEOUT, true);
//                if (String.IsNullOrWhiteSpace(resp))
//                    throw new Exception("No response from GSTD.. service may not be running");

//                //parse response
//                dynamic data = JsonSerialization.Deserialize(resp);
//                int code = data.code != null ? (int)data.code : 1;
//                string description = data.description != null ? (string)data.description : "";

//                //result
//                result = new VideoResult(code, description);
//            }
//            catch (Exception ex)
//            {
//                _errorHandler?.LogError(ex);
//                result = new VideoResult(1, ex.Message);
//            }
//            finally
//            {
//                _serviceStats.LogOperation("VideoHandler.__StartStream", sw.Elapsed);
//            }
//        }

//        /// <summary>
//        /// Stops a video stream (pipeline_stop).
//        /// </summary>
//        private string StopStream(SimpleHttpContext context)
//        {
//            try
//            {
//                //message
//                Log.WriteMessage("Video", "Executing service command '/video/stopstream'");

//                //vars
//                string name = context.Query.Get("name") ?? "";

//                //stop stream
//                __StopStream(name, out VideoResult result);

//                //build response
//                StringBuilder json = new StringBuilder();
//                using (SimpleJsonWriter writer = new SimpleJsonWriter(json))
//                {
//                    writer.WriteStartObject();
//                    WriteServiceObject(writer);
//                    WriteDeviceObject(writer);
//                    WriteRequestObject(writer, context);
//                    writer.WriteStartObject("output");
//                    writer.WritePropertyValue("success", result.Success ? 1 : 0);
//                    writer.WritePropertyValue("code", result.Code);
//                    writer.WritePropertyValue("message", result.Message);
//                    writer.WriteStartObject("stream");
//                    writer.WritePropertyValue("name", name);
//                    writer.WriteEndObject();
//                    writer.WriteEndObject();
//                    writer.WriteEndObject();
//                }
//                return json.ToString();
//            }
//            catch (Exception ex)
//            {
//                _errorHandler?.LogError(ex);
//                return WriteFatalResponse(context, ex);
//            }
//        }

//        /// <summary>
//        /// Stops a video stream (pipeline_stop).
//        /// </summary>
//        private void __StopStream(string name, out VideoResult result)
//        {
//            Stopwatch sw = Stopwatch.StartNew();
//            try
//            {
//                //return if windows
//                if (_config.IsWindows)
//                {
//                    result = new VideoResult(1, "No Windows OS support");
//                    return;
//                }

//                //validate name
//                if ((String.IsNullOrWhiteSpace(name)) || (!Regex.IsMatch(name, "^[a-zA-Z][a-zA-Z0-9]*$")))
//                    throw new Exception("Stream name is invalid");
//                lock (_streams)
//                {
//                    if (!_streams.ContainsKey(name))
//                        throw new Exception("Stream name is not registered");
//                }

//                //execute
//                string command = $"gstd-client pipeline_stop {name}";
//                string resp = command.Bash(GSTD_COMMAND_TIMEOUT, true);
//                if (String.IsNullOrWhiteSpace(resp))
//                    throw new Exception("No response from GSTD.. service may not be running");

//                //parse response
//                dynamic data = JsonSerialization.Deserialize(resp);
//                int code = data.code != null ? (int)data.code : 1;
//                string description = data.description != null ? (string)data.description : "";

//                //result
//                result = new VideoResult(code, description);
//            }
//            catch (Exception ex)
//            {
//                _errorHandler?.LogError(ex);
//                result = new VideoResult(1, ex.Message);
//            }
//            finally
//            {
//                _serviceStats.LogOperation("VideoHandler.__StopStream", sw.Elapsed);
//            }
//        }

//        /// <summary>
//        /// Pauses a video stream (pipeline_pause).
//        /// </summary>
//        private string PauseStream(SimpleHttpContext context)
//        {
//            try
//            {
//                //message
//                Log.WriteMessage("Video", "Executing service command '/video/pausestream'");

//                //vars
//                string name = context.Query.Get("name") ?? "";

//                //pause stream
//                __PauseStream(name, out VideoResult result);

//                //build response
//                StringBuilder json = new StringBuilder();
//                using (SimpleJsonWriter writer = new SimpleJsonWriter(json))
//                {
//                    writer.WriteStartObject();
//                    WriteServiceObject(writer);
//                    WriteDeviceObject(writer);
//                    WriteRequestObject(writer, context);
//                    writer.WriteStartObject("output");
//                    writer.WritePropertyValue("success", result.Success ? 1 : 0);
//                    writer.WritePropertyValue("code", result.Code);
//                    writer.WritePropertyValue("message", result.Message);
//                    writer.WriteStartObject("stream");
//                    writer.WritePropertyValue("name", name);
//                    writer.WriteEndObject();
//                    writer.WriteEndObject();
//                    writer.WriteEndObject();
//                }
//                return json.ToString();
//            }
//            catch (Exception ex)
//            {
//                _errorHandler?.LogError(ex);
//                return WriteFatalResponse(context, ex);
//            }
//        }

//        /// <summary>
//        /// Pauses a video stream (pipeline_pause).
//        /// </summary>
//        private void __PauseStream(string name, out VideoResult result)
//        {
//            Stopwatch sw = Stopwatch.StartNew();
//            try
//            {
//                //return if windows
//                if (_config.IsWindows)
//                {
//                    result = new VideoResult(1, "No Windows OS support");
//                    return;
//                }

//                //validate name
//                if ((String.IsNullOrWhiteSpace(name)) || (!Regex.IsMatch(name, "^[a-zA-Z][a-zA-Z0-9]*$")))
//                    throw new Exception("Stream name is invalid");
//                lock (_streams)
//                {
//                    if (!_streams.ContainsKey(name))
//                        throw new Exception("Stream name is not registered");
//                }

//                //execute
//                string command = $"gstd-client pipeline_pause {name}";
//                string resp = command.Bash(GSTD_COMMAND_TIMEOUT, true);
//                if (String.IsNullOrWhiteSpace(resp))
//                    throw new Exception("No response from GSTD.. service may not be running");

//                //parse response
//                dynamic data = JsonSerialization.Deserialize(resp);
//                int code = data.code != null ? (int)data.code : 1;
//                string description = data.description != null ? (string)data.description : "";

//                //result
//                result = new VideoResult(code, description);
//            }
//            catch (Exception ex)
//            {
//                _errorHandler?.LogError(ex);
//                result = new VideoResult(1, ex.Message);
//            }
//            finally
//            {
//                _serviceStats.LogOperation("VideoHandler.__StopStream", sw.Elapsed);
//            }
//        }

//        /// <summary>
//        /// Deletes a video stream (pipeline_delete).
//        /// </summary>
//        private string DeleteStream(SimpleHttpContext context)
//        {
//            try
//            {
//                //message
//                Log.WriteMessage("Video", "Executing service command '/video/deletestream'");

//                //vars
//                string name = context.Query.Get("name") ?? "";

//                //delete stream
//                __DeleteStream(name, out VideoResult result);

//                //build response
//                StringBuilder json = new StringBuilder();
//                using (SimpleJsonWriter writer = new SimpleJsonWriter(json))
//                {
//                    writer.WriteStartObject();
//                    WriteServiceObject(writer);
//                    WriteDeviceObject(writer);
//                    WriteRequestObject(writer, context);
//                    writer.WriteStartObject("output");
//                    writer.WritePropertyValue("success", result.Success ? 1 : 0);
//                    writer.WritePropertyValue("code", result.Code);
//                    writer.WritePropertyValue("message", result.Message);
//                    writer.WriteStartObject("stream");
//                    writer.WritePropertyValue("name", name);
//                    writer.WriteEndObject();
//                    writer.WriteEndObject();
//                    writer.WriteEndObject();
//                }
//                return json.ToString();
//            }
//            catch (Exception ex)
//            {
//                _errorHandler?.LogError(ex);
//                return WriteFatalResponse(context, ex);
//            }
//        }

//        /// <summary>
//        /// Deletes a video stream (pipeline_delete).
//        /// </summary>
//        private void __DeleteStream(string name, out VideoResult result)
//        {
//            Stopwatch sw = Stopwatch.StartNew();
//            try
//            {
//                //return if windows
//                if (_config.IsWindows)
//                {
//                    result = new VideoResult(1, "No Windows OS support");
//                    return;
//                }

//                //validate name
//                if ((String.IsNullOrWhiteSpace(name)) || (!Regex.IsMatch(name, "^[a-zA-Z][a-zA-Z0-9]*$")))
//                    throw new Exception("Stream name is invalid");

//                //execute
//                string command = $"gstd-client pipeline_delete {name}";
//                string resp = command.Bash(GSTD_COMMAND_TIMEOUT, true);
//                if (String.IsNullOrWhiteSpace(resp))
//                    throw new Exception("No response from GSTD.. service may not be running");

//                //parse response
//                dynamic data = JsonSerialization.Deserialize(resp);
//                int code = data.code != null ? (int)data.code : 1;
//                string description = data.description != null ? (string)data.description : "";

//                //remove locally
//                if (code == 0)
//                {
//                    lock (_streams)
//                    {
//                        if (_streams.ContainsKey(name))
//                            _streams.Remove(name);
//                    }
//                }

//                //result
//                result = new VideoResult(code, description);
//            }
//            catch (Exception ex)
//            {
//                _errorHandler?.LogError(ex);
//                result = new VideoResult(1, ex.Message);
//            }
//            finally
//            {
//                _serviceStats.LogOperation("VideoHandler.__DeleteStream", sw.Elapsed);
//            }
//        }

//        /// <summary>
//        /// Deletes all running GSTD streams.
//        /// </summary>
//        private int __DeleteAllStreams()
//        {
//            Stopwatch sw = Stopwatch.StartNew();
//            try
//            {
//                //return if windows
//                if (_config.IsWindows)
//                    return 0;

//                //message
//                Log.WriteMessage("Video", "Deleting all streams on service start..");

//                //execute
//                string command = "gstd-client list_pipelines";
//                string resp = command.Bash(GSTD_COMMAND_TIMEOUT, true);
//                if (String.IsNullOrWhiteSpace(resp))
//                    throw new Exception("No response from GSTD.. service may not be running");

//                //parse response
//                dynamic data = JsonSerialization.Deserialize(resp);
//                int code = data.code != null ? (int)data.code : 1;
//                string description = data.description != null ? (string)data.description : "";
//                List<string> daemonStreams = new List<string>();
//                if ((data.response != null) && (data.response.nodes != null))
//                {
//                    foreach (dynamic node in data.response.nodes)
//                    {
//                        string name = node.name != null ? (string)node.name : null;
//                        if (name != null)
//                            daemonStreams.Add(name);
//                    }
//                }

//                //message
//                Log.WriteMessage("Video", $"Found {daemonStreams.Count} registered streams to be deleted..");

//                //loop through streams
//                int count = 0;
//                foreach (string name in daemonStreams)
//                {
//                    try
//                    {
//                        //message
//                        Log.WriteMessage("Video", $"Deleting stream '{name}'..");

//                        //execute
//                        command = $"gstd-client pipeline_delete {name}";
//                        resp = command.Bash(GSTD_COMMAND_TIMEOUT, true);
//                        if (String.IsNullOrWhiteSpace(resp))
//                            throw new Exception("No response from GSTD.. service may not be running");

//                        //parse response
//                        data = JsonSerialization.Deserialize(resp);
//                        code = data.code != null ? (int)data.code : 1;
//                        description = data.description != null ? (string)data.description : "";

//                        //increment
//                        if (code == 0)
//                            count++;
//                    }
//                    catch (Exception ex)
//                    {
//                        _errorHandler?.LogError(ex);
//                    }
//                }

//                //clear local list
//                lock (_streams)
//                {
//                    _streams.Clear();
//                }

//                //return
//                return count;
//            }
//            catch (Exception ex)
//            {
//                _errorHandler?.LogError(ex);
//                return 0;
//            }
//            finally
//            {
//                _serviceStats.LogOperation("VideoHandler.__DeleteAllStreams", sw.Elapsed);
//            }
//        }

//        #endregion

//        #region GSTD Command Builder

//        /// <summary>
//        /// Builds the command to register a new video stream with GSTD.
//        /// This logic will change and improve over time.
//        /// </summary>
//        private string GstdCommand_PipelineCreate(string name, string device, string codec, int width, int height, int flip, string quality, int port)
//        {
//            if (!SUPPORTED_VIDEO_DEVICES.Contains(device))
//                throw new Exception("Device unknown or unsupported");

//            string command = null;

//            //mjpeg
//            if (codec == "mjpeg")
//            {
//                int jpegQuality;
//                if (quality == "high")
//                    jpegQuality = 85;
//                else if (quality == "medium")
//                    jpegQuality = 65;
//                else if (quality == "low")
//                    jpegQuality = 45;
//                else
//                    throw new Exception("Video quality is invalid");

//                //nvidia
//                if (device == "nvidia")
//                {
//                    command = "gstd-client pipeline_create $NAME nvcamerasrc intent=3 ! nvvidconv flip-method=$FLIP ! 'video/x-raw(memory:NVMM), width=(int)$WIDTH, height=(int)$HEIGHT, format=(string)I420, framerate=(fraction)30/1' ! nvjpegenc quality=$QUALITY ! image/jpeg, mapping=/$NAME ! rtspsink service=$PORT"
//                        .Replace("$NAME", name)
//                        .Replace("$FLIP", flip.ToString())
//                        .Replace("$WIDTH", width.ToString())
//                        .Replace("$HEIGHT", height.ToString())
//                        .Replace("$QUALITY", jpegQuality.ToString())
//                        .Replace("$NAME", name)
//                        .Replace("$PORT", port.ToString());
//                }

//                //video (any)
//                else if (device.Contains("video"))
//                {
//                    int deviceIndex = Int32.Parse(device.Replace("video", String.Empty));
//                    bool isAnalog = (deviceIndex >= 100) && (deviceIndex <= 199);

//                    //analog (video100-video199)
//                    if (isAnalog)
//                    {
//                        command = "gstd-client pipeline_create $NAME v4l2src device='/dev/$DEVICE' ! 'video/x-raw, width=(int)$WIDTH, height=(int)$HEIGHT, format=(string)UYVY' ! nvvidconv flip-method=$FLIP ! 'video/x-raw(memory:NVMM), width=(int)$WIDTH, height=(int)$HEIGHT, format=(string)I420' ! nvjpegenc quality=$QUALITY ! image/jpeg, mapping=/$NAME ! rtspsink service=$PORT"
//                            .Replace("$NAME", name)
//                            .Replace("$DEVICE", device)
//                            .Replace("$FLIP", flip.ToString())
//                            .Replace("$WIDTH", width.ToString())
//                            .Replace("$HEIGHT", height.ToString())
//                            .Replace("$QUALITY", jpegQuality.ToString())
//                            .Replace("$NAME", name)
//                            .Replace("$PORT", port.ToString());
//                    }

//                    //digital (others)
//                    else
//                    {
//                        //command = "gstd-client pipeline_create $NAME nvcamerasrc sensor-id='$DEVICE_INDEX' fpsRange='30 30' ! 'video/x-raw(memory:NVMM), width=(int)$WIDTH, height=(int)$HEIGHT, framerate=(fraction)30/1' ! nvvidconv flip-method=$FLIP ! nvjpegenc quality=$QUALITY ! image/jpeg, mapping=/$NAME ! rtspsink service=$PORT"
//                        command = "gstd-client pipeline_create $NAME nvcamerasrc intent=3 sensor-id='$DEVICE_INDEX' ! nvvidconv flip-method=$FLIP ! 'video/x-raw(memory:NVMM), width=(int)$WIDTH, height=(int)$HEIGHT, format=(string)I420, framerate=(fraction)30/1' ! nvjpegenc quality=$QUALITY ! image/jpeg, mapping=/$NAME ! rtspsink service=$PORT"
//                            .Replace("$NAME", name)
//                            .Replace("$DEVICE_INDEX", deviceIndex.ToString())
//                            .Replace("$FLIP", flip.ToString())
//                            .Replace("$WIDTH", width.ToString())
//                            .Replace("$HEIGHT", height.ToString())
//                            .Replace("$QUALITY", jpegQuality.ToString())
//                            .Replace("$NAME", name)
//                            .Replace("$PORT", port.ToString());
//                    }
//                }
//            }
//            else
//            {
//                throw new Exception("Codec unknown or unsupported");
//            }


//            //bool isAnalog = false;
//            //string firstPart;
//            //if (device == "nvidia")
//            //{
//            //    firstPart = "gstd-client pipeline_create $NAME nvcamerasrc intent=3 ! nvvidconv flip-method=$FLIP"
//            //        .Replace("$NAME", name)
//            //        .Replace("$FLIP", "0");
//            //}
//            //else
//            //{
//            //    int deviceIndex = Int32.Parse(device.Replace("video", String.Empty));
//            //    isAnalog = (deviceIndex >= 100) && (deviceIndex <= 199);

//            //    firstPart = "gstd-client pipeline_create $NAME v4l2src device='/dev/$DEVICE' ! 'video/x-raw, width=(int)$WIDTH, height=(int)$HEIGHT, format=(string)UYVY' ! nvvidconv flip-method=$FLIP"
//            //        .Replace("$NAME", name)
//            //        .Replace("$DEVICE", device)
//            //        .Replace("$WIDTH", width.ToString())
//            //        .Replace("$HEIGHT", height.ToString())
//            //        .Replace("$FLIP", flip.ToString());
//            //}

//            //string secondPart;
//            //if (codec == "mjpeg")
//            //{
//            //    int jpegQuality;
//            //    if (quality == "high")
//            //        jpegQuality = 85;
//            //    else if (quality == "medium")
//            //        jpegQuality = 65;
//            //    else if (quality == "low")
//            //        jpegQuality = 45;
//            //    else
//            //        throw new Exception("Video quality is invalid");

//            //    string framerate = !isAnalog ? ", framerate=(fraction)30/1" : String.Empty;

//            //    secondPart = " ! 'video/x-raw(memory:NVMM), width=(int)$WIDTH, height=(int)$HEIGHT, format=(string)I420$FRAMERATE' ! nvjpegenc quality=$QUALITY ! image/jpeg, mapping=/$NAME ! rtspsink service=$PORT"
//            //        .Replace("$WIDTH", width.ToString())
//            //        .Replace("$HEIGHT", height.ToString())
//            //        .Replace("$FRAMERATE", framerate)
//            //        .Replace("$QUALITY", jpegQuality.ToString())
//            //        .Replace("$NAME", name)
//            //        .Replace("$PORT", port.ToString());
//            //}
//            //else
//            //{
//            //    throw new Exception("Codec unknown or unsupported");
//            //}

//            //string command = firstPart + secondPart;
//            return command;
//        }

//        #endregion

//        #region Misc

//        /// <summary>
//        /// Returns the next unused RTSP port at random.
//        /// </summary>
//        private int GetNextPort()
//        {
//            int port;
//            Random r = new Random();
//            lock (_streams)
//            {
//                List<int> portsInUse = _streams.Values.Select(s => s.Port).ToList();
//                if (portsInUse.Count >= 50)
//                    throw new Exception("Too many video streams");
//                do
//                {
//                    port = r.Next(RTSP_PORT_START, RTSP_PORT_END);
//                }
//                while (portsInUse.Contains(port));
//            }
//            return port;
//        }

//        /// <summary>
//        /// Converts the GSTD pipeline state string to our state string.
//        /// </summary>
//        private string StateToState(string state)
//        {
//            state = (state ?? String.Empty).ToLower();
//            if ((String.IsNullOrWhiteSpace(state)) || (state == "null"))
//                return "stopped";
//            if (state == "playing")
//                return "started";
//            if (state == "paused")
//                return "paused";
//            return "unknown";
//        }

//        /// <summary>
//        /// Fired by timer to refresh stream data.
//        /// </summary>
//        private void RefreshTimer_Callback()
//        {
//            try
//            {
//                //queries GSTD and syncs data
//                __ListStreams(out VideoResult result, false);
//            }
//            catch (Exception ex)
//            {
//                _errorHandler?.LogError(ex);
//            }
//        }

//        /// <summary>
//        /// Returns read-only copy of last known stream list.
//        /// </summary>
//        public IReadOnlyList<VideoStream> GetStreams()
//        {
//            List<VideoStream> streams;
            
//            try
//            {
//                lock (_streams)
//                {
//                    streams = _streams.Values.OrderBy(s => s.Name).ToList();
//                }
//            }
//            catch (Exception ex)
//            {
//                _errorHandler?.LogError(ex);
//                streams = new List<VideoStream>();
//            }

//            return streams;
//        }

//        #endregion

//        #region Statistics

//        /// <summary>
//        /// Writes runtime statistics.
//        /// </summary>
//        public void WriteRuntimeStatistics(SimpleJsonWriter writer)
//        {
//            try
//            {
//                IReadOnlyList<VideoStream> streams = GetStreams();
//                writer.WriteStartObject("videoHandler");
//                writer.WriteStartArray("streams");
//                foreach (VideoStream stream in streams)
//                {
//                    writer.WriteStartObject();
//                    writer.WritePropertyValue("name", stream.Name);
//                    writer.WritePropertyValue("device", stream.Device);
//                    writer.WritePropertyValue("codec", stream.Codec);
//                    writer.WritePropertyValue("width", stream.Width);
//                    writer.WritePropertyValue("height", stream.Height);
//                    writer.WritePropertyValue("flip", stream.Flip);
//                    writer.WritePropertyValue("quality", stream.Quality);
//                    writer.WritePropertyValue("ipAddress", stream.IPAddress);
//                    writer.WritePropertyValue("port", stream.Port);
//                    writer.WritePropertyValue("state", stream.State);
//                    writer.WriteEndObject();
//                }
//                writer.WriteEndArray();
//                writer.WriteEndObject();
//            }
//            catch (Exception ex)
//            {
//                _errorHandler?.LogError(ex);
//            }
//        }

//        #endregion
//    }

//    #region Classes

//    /// <summary>
//    /// Stores properties about a stream.
//    /// </summary>
//    public class VideoStream
//    {
//        public string Name { get; }
//        public string Device { get; }
//        public string Codec { get; }
//        public int Width { get; }
//        public int Height { get; }
//        public int Flip { get; }
//        public string Quality { get; }
//        public string IPAddress { get; }
//        public int Port { get; }
//        public string State { get; set; }

//        public VideoStream(string name, string device, string codec, int width, int height, int flip, string quality, string ipAddress, int port, string state)
//        {
//            Name = name;
//            Device = device;
//            Codec = codec;
//            Width = width;
//            Height = height;
//            Flip = flip;
//            Quality = quality;
//            IPAddress = ipAddress;
//            Port = port;
//            State = state;
//        }
//    }

//    /// <summary>
//    /// Stores a command result.
//    /// </summary>
//    public class VideoResult
//    {
//        public bool Success { get; }
//        public int Code { get; }
//        public string Message { get; }

//        public VideoResult(int code, string message)
//        {
//            Success = code == 0;
//            Code = code;
//            Message = message;
//        }
//    }

//    /// <summary>
//    /// The 'flip-method' parameter values supported by the 'nvvidconv' plugin.
//    /// </summary>
//    public enum FlipMethod
//    {
//        NoRotation = 0,
//        CounterClockwise90Degrees = 1,
//        Rotate180Degrees = 2,
//        Clockwise90Degrees = 3,
//        HorizontalFlip = 4,
//        UpperRightDiagonalFlip = 5,
//        VerticalFlip = 6,
//        UpperLeftDiagonalFlip = 7
//    }

//    #endregion

//}
