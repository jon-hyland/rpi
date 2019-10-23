using Microsoft.AspNetCore.Http;
using Rpi.Configuration;
using Rpi.Error;
using Rpi.Extensions;
using Rpi.Http;
using Rpi.Json;
using Rpi.Output;
using Rpi.Service;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rpi.Handlers
{
    /// <summary>
    /// Handles requests for statistics.
    /// </summary>
    public class StatisticsHandler : HandlerBase
    {
        //private
        private readonly List<IStatsWriter> _statsWriters;

        /// <summary>
        /// Class constructor.
        /// </summary>
        public StatisticsHandler(IErrorHandler errorHandler, IConfig config, ServiceStats serviceStats, List<IStatsWriter> statsWriters)
            : base(errorHandler, config, serviceStats)
        {
            _statsWriters = statsWriters;
        }

        /// <summary>
        /// Called before HTTP listener started.
        /// </summary>
        public override void PreInitialize()
        {
        }

        /// <summary>
        /// Called after HTTP listener started.
        /// </summary>
        public override void PostInitialize()
        {
        }

        /// <summary>
        /// Handles HTTP request.
        /// </summary>
        protected override async Task HandleRequest(SimpleHttpContext context)
        {
            string json, html, text;
            switch (context.Command)
            {
                case "ping":
                    json = Ping(context);
                    await context.WriteJson(json);
                    break;

                case "getstats":
                    json = GetStats(context);
                    await context.WriteJson(json);
                    break;

                case "getstatshtml":
                    json = GetStats(context);
                    html = StatsToHtml.GetStatsHtml(json);
                    await context.WriteHtml(html);
                    break;

                case "getlogs":
                    text = GetLogs(context);
                    await context.WriteText(text);
                    break;

                case "pstree":
                    text = PsTree(context);
                    context.Response.ContentType = "text/plain";
                    await context.Response.WriteAsync(text);
                    break;

                case "ps":
                    text = Ps(context);
                    context.Response.ContentType = "text/plain";
                    await context.Response.WriteAsync(text);
                    break;

                default:
                    string message = "Command not found";
                    await context.WriteError(message, 404);
                    throw new Exception(message);
            }            
        }

        /// <summary>
        /// Executes 'Ping' command.
        /// </summary>
        private string Ping(SimpleHttpContext context)
        {
            StringBuilder json = new StringBuilder();
            try
            {
                using (var writer = new SimpleJsonWriter(json))
                {
                    writer.WriteStartObject();
                    WriteServiceObject(writer, true);
                    WriteDeviceObject(writer);
                    _statsWriters
                        .Where(sw => sw is ServiceState)
                        .FirstOrDefault()?
                        .WriteRuntimeStatistics(writer);
                    writer.WriteEndObject();
                }
            }
            catch (Exception ex)
            {
                _errorHandler?.LogError(ex);
                return WriteFatalResponse(context, ex);
            }
            return json.ToString();
        }

        /// <summary>
        /// Executes 'GetStats' command.
        /// </summary>
        private string GetStats(SimpleHttpContext context)
        {
            StringBuilder json = new StringBuilder();
            try
            {
                using (SimpleJsonWriter writer = new SimpleJsonWriter(json))
                {
                    writer.WriteStartObject();
                    WriteServiceObject(writer, true);
                    WriteDeviceObject(writer);
                    WriteRequestObject(writer, context);
                    _statsWriters
                        .ToList()
                        .ForEach(w => w.WriteRuntimeStatistics(writer));
                    writer.WriteEndObject();
                }
            }
            catch (Exception ex)
            {
                _errorHandler?.LogError(ex);
                return WriteFatalResponse(context, ex);
            }
            return json.ToString();
        }

        /// <summary>
        /// Executes 'GetLogs' command.
        /// </summary>
        private string GetLogs(SimpleHttpContext context)
        {
            StringBuilder text = new StringBuilder();
            try
            {
                Int32.TryParse(context.Query.Get("maxLines"), out int maxLines);
                if (maxLines <= 0)
                    maxLines = 1000;

                Double.TryParse(context.Query.Get("minutes"), out double minutes);
                if (minutes == 0)
                    minutes = 1440 * 7;
                if (minutes > 1000000)
                    minutes = 1000000;

                List<string> lines = Log.GetLog(maxLines, minutes);
                lines.ForEach(l => text.AppendLine(l));
            } 
            catch (Exception ex)
            {
                _errorHandler?.LogError(ex);
                return ex.Message;
            }
            return text.ToString();
        }

        /// <summary>
        /// Executes 'pstree' command.
        /// </summary>
        private string PsTree(SimpleHttpContext context)
        {
            try
            {
                if (_config.IsWindows)
                    return "No Windows OS support";

                string args = context.Query.Get("args") ?? "-cg";
                string data = $"pstree {args}".Bash(2500, true);

                return data;
            }
            catch (Exception ex)
            {
                _errorHandler?.LogError(ex);
            }
            return "";
        }

        /// <summary>
        /// Executes 'ps' command.
        /// </summary>
        private string Ps(SimpleHttpContext context)
        {
            try
            {
                if (_config.IsWindows)
                    return "No Windows OS support";

                string args = context.Query.Get("args") ?? "-e -o pid,uname,pcpu,pmem,comm --sort -pcpu";
                string data = $"ps {args}".Bash(2500, true);

                return data;
            }
            catch (Exception ex)
            {
                _errorHandler?.LogError(ex);
            }
            return "";
        }

    }
}
