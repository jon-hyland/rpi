using System;
using Microsoft.Extensions.Configuration;
using Rpi.Health;

namespace Rpi.Configuration
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
        int ListenPort { get; }
        TimeSpan ErrorRetention { get; }
        string[] InterfaceNames { get; }
        int PollingIntervalMs { get; }
        int StickyHighInputMs { get; }
        Interface PrimaryInterface { get; set; }
        
        string GetOS();
        int GetMegabytesUsedByProcess();
    }
}