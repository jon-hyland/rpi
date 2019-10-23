using Rpi.Error;
using Rpi.Gpio;
using Rpi.Http;
using Rpi.Json;
using Rpi.Service;
using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Rpi.Handlers
{
    /// <summary>
    /// Handles requests for RPI GPIO.
    /// </summary>
    public class GpioHandler : HandlerBase
    {
        //private
        private readonly GpioManager _gpio = null;

        /// <summary>
        /// Class constructor.
        /// </summary>
        public GpioHandler(IErrorHandler errorHandler, IConfig config, ServiceStats serviceStats, GpioManager gpio)
            : base(errorHandler, config, serviceStats)
        {
            _gpio = gpio;
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
                case "read":
                    json = Read(context);
                    await context.WriteJson(json);
                    break;

                case "write":
                    json = Write(context);
                    await context.WriteJson(json);
                    break;

                case "readwrite":
                    //json = ReadWrite(context);
                    json = @"{ ""output"": { ""success"": 1, ""input1"": ""00000000"", ""input2"": ""00000000"", ""output"": ""00000000"" } }";
                    await context.WriteJson(json);
                    break;

                default:
                    string message = "Command not found";
                    await context.WriteError(message, 404);
                    throw new Exception(message);
            }            
        }

        /// <summary>
        /// Executes 'Read' command.
        /// </summary>
        private string Read(SimpleHttpContext context)
        {
            StringBuilder json = new StringBuilder();
            try
            {
                string input1 = _gpio.GetBank(BankType.Input1);
                string input2 = _gpio.GetBank(BankType.Input2);
                string output = _gpio.GetBank(BankType.Output);

                using (SimpleJsonWriter writer = new SimpleJsonWriter(json))
                {
                    writer.WriteStartObject();
                    WriteServiceObject(writer, true);
                    WriteDeviceObject(writer);
                    WriteRequestObject(writer, context);
                    writer.WriteStartObject("output");
                    writer.WritePropertyValue("success", 1);
                    writer.WritePropertyValue("input1", input1);
                    writer.WritePropertyValue("input2", input2);
                    writer.WritePropertyValue("output", output);
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

        /// <summary>
        /// Executes 'Write' command.
        /// </summary>
        private string Write(SimpleHttpContext context)
        {
            StringBuilder json = new StringBuilder();
            try
            {
                string output = context.Query.Get("output");
                if (String.IsNullOrWhiteSpace(output))
                    throw new Exception("Parameter 'output' missing or invalid");
                _gpio.SetBank(BankType.Output, output);

                using (SimpleJsonWriter writer = new SimpleJsonWriter(json))
                {
                    writer.WriteStartObject();
                    WriteServiceObject(writer, true);
                    WriteDeviceObject(writer);
                    WriteRequestObject(writer, context);
                    writer.WriteStartObject("output");
                    writer.WritePropertyValue("success", 1);
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

        /// <summary>
        /// Executes 'Read Write' command.
        /// </summary>
        private string ReadWrite(SimpleHttpContext context)
        {
            StringBuilder json = new StringBuilder();
            try
            {
                string input1 = _gpio.GetBank(BankType.Input1);
                string input2 = _gpio.GetBank(BankType.Input2);
                string outputRead = _gpio.GetBank(BankType.Output);

                string outputWrite = context.Query.Get("output");
                if (String.IsNullOrWhiteSpace(outputWrite))
                    throw new Exception("Parameter 'output' is missing or invalid");
                _gpio.SetBank(BankType.Output, outputWrite);

                using (SimpleJsonWriter writer = new SimpleJsonWriter(json))
                {
                    writer.WriteStartObject();
                    WriteServiceObject(writer, true);
                    WriteDeviceObject(writer);
                    WriteRequestObject(writer, context);
                    writer.WriteStartObject("output");
                    writer.WritePropertyValue("success", 1);
                    writer.WritePropertyValue("input1", input1);
                    writer.WritePropertyValue("input2", input2);
                    writer.WritePropertyValue("output", outputRead);
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
