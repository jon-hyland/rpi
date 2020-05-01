using Rpi.Common.Configuration;
using Rpi.Common.Error;
using Rpi.Common.Extensions;
using Rpi.Common.Http;
using Rpi.Common.Json;
using Rpi.Common.Output;
using Rpi.Common.Service;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Rpi.Common.Handlers
{
    /// <summary>
    /// Base class for all path handlers.. contains some helper stuff, query parsing, etc.
    /// </summary>
    public abstract class HandlerBase : IHttpHandler
    {
        //private
        protected readonly IErrorHandler _errorHandler;
        protected readonly IConfig _config;
        protected readonly ILogger _logger;
        protected readonly ServiceStats _serviceStats;

        /// <summary>
        /// Class constructor.
        /// </summary>
        public HandlerBase(IErrorHandler errorHandler, IConfig config, ILogger logger, ServiceStats serviceStats)
        {
            _errorHandler = errorHandler;
            _config = config;
            _logger = logger;
            _serviceStats = serviceStats;
        }

        /// <summary>
        /// Called before HTTP listener started.
        /// </summary>
        public abstract void PreInitialize();

        /// <summary>
        /// Called after HTTP listener started.
        /// </summary>
        public abstract void PostInitialize();

        /// <summary>
        /// Handles an HTTP request, performs some helper stuff, passes execution to instance method.
        /// </summary>
        public async Task ProcessRequest(SimpleHttpContext context)
        {
            try
            {
                string log = context.Query.Get("log") ?? "1";
                if ((log == "1") || (log == "true"))
                    _logger?.WriteMessage("Http", $"Handling request: {context.Url}");

                await HandleRequest(context);
                _serviceStats.LogCommand(context.Path, context.Stopwatch.Elapsed, true);
            }
            catch (Exception ex)
            {
                _errorHandler?.LogError(ex);
                throw;
            }
        }

        /// <summary>
        /// Instance method to handle specific path request.
        /// </summary>
        protected abstract Task HandleRequest(SimpleHttpContext context);

        /// <summary>
        /// Writes fatal error response as JSON if command execution failed completely.
        /// </summary>
        protected string WriteFatalResponse(SimpleHttpContext context, Exception ex)
        {
            StringBuilder json = new StringBuilder();
            try
            {
                using (SimpleJsonWriter writer = new SimpleJsonWriter(json))
                {
                    writer.WriteStartObject();
                    WriteServiceObject(writer);
                    WriteDeviceObject(writer);
                    WriteRequestObject(writer, context);
                    writer.WriteStartObject("output");
                    writer.WritePropertyValue("success", 0);
                    writer.WritePropertyValue("code", 1);
                    writer.WritePropertyValue("message", ex.Message ?? "A fatal error occurred");
                    writer.WriteEndObject();
                    WriteErrorsObject(writer, new List<Exception>() { ex });
                    writer.WriteEndObject();
                }
            }
            catch (Exception exx)
            {
                _errorHandler?.LogError(exx);
            }
            return json.ToString();
        }

        /// <summary>
        /// Writes the 'service' object for all commands.
        /// </summary>
        public void WriteServiceObject(SimpleJsonWriter writer, bool isStatistics = false)
        {
            writer.WriteStartObject("service");
            writer.WritePropertyValue("name", _config.ServiceName);
            writer.WritePropertyValue("version", _config.ServiceVersion);
            if (isStatistics)
            {
                writer.WritePropertyValue("now", DateTime.Now.ToString());
                writer.WritePropertyValue("runningTime", _config.RunningTime.ToShortString());
                writer.WritePropertyValue("memoryUsageMB", _config.GetMegabytesUsedByProcess());
            }
            writer.WriteEndObject();
        }

        /// <summary>
        /// Writes the 'device' object for all commands.
        /// </summary>
        public void WriteDeviceObject(SimpleJsonWriter writer)
        {
            writer.WriteStartObject("device");
            writer.WritePropertyValue("serial", _config.DeviceSerial ?? "");
            writer.WritePropertyValue("name", _config.DeviceName ?? "");
            writer.WritePropertyValue("interfaceName", _config.PrimaryInterface?.Name ?? "");
            writer.WritePropertyValue("macAddress", _config.PrimaryInterface?.PhysicalAddress ?? "");
            writer.WritePropertyValue("ipAddress", _config.PrimaryInterface?.InternetAddress ?? "");
            writer.WritePropertyValue("os", _config.GetOS());
            writer.WriteEndObject();
        }

        /// <summary>
        /// Writes the 'request' object for all commands.
        /// </summary>
        public void WriteRequestObject(SimpleJsonWriter writer, SimpleHttpContext context)
        {
            IDictionary<string, string[]> qs = context.Query.GetAll();
            writer.WriteStartObject("request");
            writer.WritePropertyValue("handler", context.Handler);
            writer.WritePropertyValue("command", context.Command);
            foreach (string key in qs.Keys)
                writer.WritePropertyValue(key, qs[key].Length > 0 ? qs[key][0] : null);
            writer.WritePropertyValue("elapsedMS", context.Stopwatch.ElapsedMilliseconds);
            writer.WriteEndObject();
        }

        /// <summary>
        /// Writes the 'errors' object for all commands.
        /// </summary>
        public void WriteErrorsObject(SimpleJsonWriter writer, List<Exception> errors)
        {
            writer.WriteStartArray("errors");
            if ((errors != null) && (errors.Count > 0))
            {
                for (int i = 0; i < errors.Count; i++)
                {
                    Exception ex = errors[i];
                    if (!String.IsNullOrEmpty(ex.Message))
                    {
                        writer.WriteStartObject();
                        writer.WritePropertyValue("type", ex.GetType().ToString());
                        writer.WritePropertyValue("message", CleanErrorString(ex.Message));
                        writer.WritePropertyValue("stack", CleanErrorString(ex.StackTrace ?? ""));
                        writer.WriteEndObject();
                    }
                }
            }
            writer.WriteEndArray();
        }

        /// <summary>
        /// Makes exception message and stack trace look better in JSON output.
        /// </summary>
        private static string CleanErrorString(string input)
        {
            string output = input ?? "";
            if (output.Contains("\r"))
                output = output.Replace("\r", " ");
            if (output.Contains("\n"))
                output = output.Replace("\n", " ");
            if (output.Contains("\t"))
                output = output.Replace("\t", " ");
            while (output.Contains("  "))
                output = output.Replace("  ", " ");
            output = output.Trim();
            return output;
        }
    }
}
