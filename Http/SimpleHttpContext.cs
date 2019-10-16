using Microsoft.AspNetCore.Http;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Rpi.Http
{
    /// <summary>
    /// Holds stuff to be passed to instance method.
    /// </summary>
    public class SimpleHttpContext
    {
        //public
        public HttpContext Context { get; }
        public HttpRequest Request { get => Context.Request; }
        public HttpResponse Response { get => Context.Response; }
        public string Handler { get; }
        public string Command { get; }
        public string Path { get => $"{Handler}.{Command}"; }
        public string Url { get => Request.Path + Request.QueryString.ToString(); }
        public QueryDictionary Query { get; }
        public Stopwatch Stopwatch { get; }
        public DateTime StartTime { get; }

        /// <summary>
        /// Class constructor.
        /// </summary>
        public SimpleHttpContext(HttpContext context)
        {
            Context = context;
            Handler = ParseHandler(context);
            Command = ParseCommand(context);
            Query = new QueryDictionary(context);
            Stopwatch = Stopwatch.StartNew();
            StartTime = DateTime.Now;
        }

        /// <summary>
        /// Parses handler from URL.
        /// </summary>
        private string ParseHandler(HttpContext context)
        {
            string path = context.Request.Path;
            if (!path.StartsWith("/"))
                path = "/" + path;
            if (!path.EndsWith("/"))
                path += "/";
            string[] split = path.Split(new char[] { '/' }, StringSplitOptions.None);
            return split.Length >= 2 ? split[1] : "";
        }

        /// <summary>
        /// Parses command from URL.
        /// </summary>
        private string ParseCommand(HttpContext context)
        {
            string path = context.Request.Path;
            if (!path.StartsWith("/"))
                path = "/" + path;
            if (!path.EndsWith("/"))
                path += "/";
            string[] split = path.Split(new char[] { '/' }, StringSplitOptions.None);
            return split.Length >= 3 ? split[2] : "";
        }

        /// <summary>
        /// Writes plain text with UTF8 encoding, sets proper content headers and status code of 200.
        /// </summary>
        public async Task WriteText(string text)
        {
            Response.StatusCode = 200;
            Response.ContentType = "text/plain; charset=utf-8";
            await Response.WriteAsync(text);
        }

        /// <summary>
        /// Writes JSON with UTF8 encoding, sets proper content headers and status code of 200.
        /// </summary>
        public async Task WriteJson(string json)
        {
            Response.StatusCode = 200;
            Response.ContentType = "application/json; charset=utf-8";
            await Response.WriteAsync(json);
        }

        /// <summary>
        /// Writes XML with UTF8 encoding, sets proper content headers and status code of 200.
        /// </summary>
        public async Task WriteXml(string xml)
        {
            Response.StatusCode = 200;
            Response.ContentType = "application/xml; charset=utf-8";
            await Response.WriteAsync(xml);
        }

        /// <summary>
        /// Writes HTML with UTF8 encoding, sets proper content headers and status code of 200.
        /// </summary>
        public async Task WriteHtml(string html)
        {
            Response.StatusCode = 200;
            Response.ContentType = "text/html; charset=utf-8";
            await Response.WriteAsync(html);
        }

        /// <summary>
        /// Writes error message with UTF8 encoding, sets proper content headers and specified status code.
        /// </summary>
        public async Task WriteError(string message, int statusCode = 500)
        {
            Response.StatusCode = statusCode;
            Response.ContentType = "text/plain; charset=utf-8";
            await Response.WriteAsync(message);
        }
    }
}
