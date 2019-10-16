using Rpi.Error;
using Rpi.Http;
using Rpi.Json;
using Rpi.Service;
using System;
using System.Text;
using System.Threading.Tasks;

namespace Rpi.Handlers
{
    /// <summary>
    /// Handles requests for configuration.
    /// </summary>
    public class ConfigHandler : HandlerBase
    {
        /// <summary>
        /// Class constructor.
        /// </summary>
        public ConfigHandler(IErrorHandler errorHandler, Config config, ServiceStats serviceStats)
            : base(errorHandler, config, serviceStats)
        {
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
            string json;
            switch (context.Command)
            {
                case "setdevicename":
                    json = SetDeviceName(context);
                    await context.WriteJson(json);
                    break;

                default:
                    string message = "Command not found";
                    await context.WriteError(message, 404);
                    throw new Exception(message);
            }            
        }

        /// <summary>
        /// Executes 'Set Device Name' command.
        /// </summary>
        private string SetDeviceName(SimpleHttpContext context)
        {
            StringBuilder json = new StringBuilder();
            try
            {
                string name = context.Query.Get("name");
                if (String.IsNullOrWhiteSpace(name))
                    throw new Exception("Parameter 'name' is missing or invalid");

                _config.DeviceName = name;
                using (var writer = new SimpleJsonWriter(json))
                {
                    writer.WriteStartObject();
                    WriteServiceObject(writer, true);
                    WriteDeviceObject(writer);
                    WriteRequestObject(writer, context);
                    writer.WriteStartObject("output");
                    writer.WritePropertyValue("success", 1);
                    writer.WritePropertyValue("code", 0);
                    writer.WriteEndObject();
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

    

    }
}
