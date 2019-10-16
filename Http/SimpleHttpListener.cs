using Rpi.Error;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

namespace Rpi.Http
{
    /// <summary>
    /// Wraps the multi-platform Kestrel web host, creating a simple generic HTTP server.
    /// </summary>
    public class SimpleHttpListener
    {
        //private
        private readonly int _port = 5001;
        private readonly IErrorHandler _errorHandler = null;
        private readonly Dictionary<string, IHttpHandler> _handlers = new Dictionary<string, IHttpHandler>();
        private readonly IWebHost _host = null;

        /// <summary>
        /// Class constructor.
        /// </summary>
        public SimpleHttpListener(int port, IErrorHandler errorHandler)
        {
            _port = port;
            _errorHandler = errorHandler;
            _host = new WebHostBuilder()
                .UseKestrel(options =>
                {
                    options.ListenAnyIP(_port);
                })
                .ConfigureServices(services =>
                {
                })
                .Configure(app =>
                {
                    app.UseMiddleware<HttpMiddleware>(new ListenerSettings()
                    {
                        Handlers = _handlers,
                        ErrorHandler = _errorHandler
                    });
                })
                .Build();
        }

        /// <summary>
        /// Maps a handler key to an IHttpHandler.
        /// </summary>
        public void RegisterHandler(string key, IHttpHandler handler)
        {
            //register handler
            if (!_handlers.ContainsKey(key))
                _handlers.Add(key, handler);
            else
                _handlers[key] = handler;
        }

        /// <summary>
        /// Returns copy of handler list.
        /// </summary>
        public IReadOnlyList<IHttpHandler> GetHandlers()
        {
            return _handlers.Values.ToList().AsReadOnly();
        }

        /// <summary>
        /// Runs as a web application and blocks caller until service shutdown.
        /// </summary>
        public void Run()
        {
            _host.Run();
        }

        /// <summary>
        /// Starts the listener and returns.
        /// </summary>
        public void Start()
        {
            _host.Start();
        }

        /// <summary>
        /// Attempt to gracefully stop the listener.
        /// </summary>
        public async Task Stop()
        {
            await _host.StopAsync();
        }

        /// <summary>
        /// The middleware module used by the underlying process to handle requests.
        /// </summary>
        private class HttpMiddleware
        {
            //private
            private readonly ListenerSettings _settings;

            /// <summary>
            /// Class constructor.
            /// </summary>
            public HttpMiddleware(RequestDelegate next, ListenerSettings settings)
            {
                _settings = settings;
            }

            /// <summary>
            /// Invoked when a request is received.
            /// </summary>
            public async Task Invoke(HttpContext c)
            {
                try
                {
                    SimpleHttpContext context = new SimpleHttpContext(c);
                    if (_settings.Handlers.ContainsKey(context.Handler))
                    {
                        IHttpHandler handler = _settings.Handlers[context.Handler];
                        await handler.ProcessRequest(context);
                    }
                    else
                    {
                        string message = "Handler not found";
                        if (_settings.ErrorHandler != null)
                            _settings.ErrorHandler.LogError(new Exception(message));
                        context.Response.StatusCode = 404;
                        context.Response.ContentType = "text/plain; charset=utf-8";
                        await context.Response.WriteAsync(message);
                    }
                }
                catch (Exception ex)
                {
                    _settings.ErrorHandler?.LogError(ex);
                }
            }
        }

        /// <summary>
        /// Stores listener settings used by underlying process.
        /// </summary>
        private class ListenerSettings
        {
            public Dictionary<string, IHttpHandler> Handlers { get; set; }
            public IErrorHandler ErrorHandler { get; set; }
        }
    }

   
}
