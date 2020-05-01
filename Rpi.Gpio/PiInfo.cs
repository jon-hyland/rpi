using Rpi.Common.Configuration;
using Rpi.Common.Json;
using Rpi.Common.Output;
using System;
using Unosquare.RaspberryIO;

namespace Rpi.Gpio
{
    public class PiInfo : IStatsWriter
    {
        //private
        private readonly IConfig _config;

        /// <summary>
        /// Class constructor.
        /// </summary>
        public PiInfo(IConfig config)
        {
            _config = config;
        }

        /// <summary>
        /// Writes runtime stats.
        /// </summary>
        public void WriteRuntimeStatistics(SimpleJsonWriter writer)
        {
            if (!_config.IsLinux)
                return;

            writer.WriteStartObject("pi");
            writer.WritePropertyValue("boardModel", Pi.Info.BoardModel.ToString());
            writer.WritePropertyValue("boardRevision", Pi.Info.BoardRevision);
            writer.WritePropertyValue("cpuArchitecture", Pi.Info.CpuArchitecture);
            writer.WritePropertyValue("cpuImplementer", Pi.Info.CpuImplementer);
            writer.WritePropertyValue("cpuPart", Pi.Info.CpuPart);
            writer.WritePropertyValue("cpuRevision", Pi.Info.CpuRevision);
            writer.WritePropertyValue("cpuVariant", Pi.Info.CpuVariant);
            writer.WritePropertyValue("features", String.Join(", ", Pi.Info.Features));
            writer.WritePropertyValue("hardware", Pi.Info.Hardware);
            writer.WritePropertyValue("installedRam", Pi.Info.InstalledRam);
            writer.WritePropertyValue("isLittleEndian", Pi.Info.IsLittleEndian ? 1 : 0);
            writer.WritePropertyValue("libraryVersion", Pi.Info.LibraryVersion?.ToString());
            writer.WritePropertyValue("manufacturer", Pi.Info.Manufacturer.ToString());
            writer.WritePropertyValue("memorySize", Pi.Info.MemorySize.ToString());
            writer.WritePropertyValue("modelName", Pi.Info.ModelName);
            writer.WritePropertyValue("operatingSystem", Pi.Info.OperatingSystem?.ToString());
            writer.WritePropertyValue("processorModel", Pi.Info.ProcessorModel.ToString());
            writer.WritePropertyValue("processorModel", Pi.Info.ProcessorModel.ToString());
            writer.WritePropertyValue("raspberryPiVersion", Pi.Info.RaspberryPiVersion.ToString());
            writer.WritePropertyValue("revision", Pi.Info.Revision);
            writer.WritePropertyValue("revisionNumber", Pi.Info.RevisionNumber);
            writer.WritePropertyValue("serial", Pi.Info.Serial);
            writer.WritePropertyValue("uptimeSecs", Pi.Info.Uptime);            
            writer.WriteEndObject();
        }
    }
}
