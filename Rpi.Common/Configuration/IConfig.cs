using Microsoft.Extensions.Configuration;
using Rpi.Common.Network;
using System;

namespace Rpi.Common.Configuration
{
    public interface IConfig
    {
        IConfigurationRoot Root { get; }
        bool IsWindows { get; }
        bool IsLinux { get; }
        bool IsOSX { get; }
        string DeviceSerial { get; }
        string DeviceName { get; set; }
        string ServiceName { get; }
        string ServiceVersion { get; }
        string ApplicationPath { get; }
        //int ListenPort { get; }
        //TimeSpan ErrorRetention { get; }
        //string[] InterfaceNames { get; }
        //int PollingIntervalMs { get; }
        //int StickyHighInputMs { get; }
        Interface PrimaryInterface { get; set; }
        TimeSpan RunningTime { get; }

        string GetOS();
        int GetMegabytesUsedByProcess();
    }
}