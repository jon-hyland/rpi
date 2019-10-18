using Rpi.Json;
using Rpi.Output;
using System;
using System.Collections.Generic;
using System.Text;
using Unosquare.RaspberryIO;

namespace Rpi
{
    public class PiInfo : IStatsWriter
    {
        /// <summary>
        /// Writes runtime stats.
        /// </summary>
        public void WriteRuntimeStatistics(SimpleJsonWriter writer)
        {
            writer.WriteStartObject("pi");
            writer.WritePropertyValue("boardModel", Pi.Info.BoardModel);
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
            writer.WritePropertyValue("libraryVersion", Pi.Info.LibraryVersion);
            writer.WritePropertyValue("manufacturer", Pi.Info.Manufacturer);
            writer.WritePropertyValue("memorySize", Pi.Info.MemorySize);
            writer.WritePropertyValue("modelName", Pi.Info.ModelName);
            writer.WritePropertyValue("operatingSystem", Pi.Info.OperatingSystem);
            writer.WritePropertyValue("processorModel", Pi.Info.ProcessorModel);
            writer.WritePropertyValue("processorModel", Pi.Info.ProcessorModel);
            writer.WritePropertyValue("raspberryPiVersion", Pi.Info.RaspberryPiVersion);
            writer.WritePropertyValue("revision", Pi.Info.Revision);
            writer.WritePropertyValue("revisionNumber", Pi.Info.RevisionNumber);
            writer.WritePropertyValue("serial", Pi.Info.Serial);
            writer.WritePropertyValue("uptimeSecs", Pi.Info.Uptime);            
            writer.WriteEndObject();
        }
    }
}
