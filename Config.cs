using Microsoft.Extensions.Configuration;
using Rpi.Health;
using Rpi.Json;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace Rpi
{
    /// <summary>
    /// Stores service configuration data.
    /// </summary>
    public class Config : IConfig
    {
        //private
        private readonly IConfigurationRoot _root;
        private readonly string _deviceSerial;
        private Interface _primaryInterface;
        private readonly ConfigStorage _storage;

        //public
        public IConfigurationRoot Root => _root;
        public bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        public bool IsOSX => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
        public bool IsLinux => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
        public string DeviceSerial => _deviceSerial;
        public string DeviceName { get => _storage.DeviceName; set { _storage.DeviceName = value; _storage.SaveSettings(); } }
        public string ServiceName => "Rpi";
        public string ServiceVersion => "1.0.20";
        public string ApplicationPath => Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        public int ListenPort => _root.GetValue("listenPort", 5001);
        public TimeSpan ErrorRetention => TimeSpan.FromMinutes(_root.GetValue("errorRetentionMins", 60));
        public string[] InterfaceNames => _root.GetSection("interfaceNames").GetChildren().Select(c => c.Value.ToLower()).ToArray();
        public Interface PrimaryInterface { get => _primaryInterface; set => _primaryInterface = value; }

        /// <summary>
        /// Class constructor.
        /// </summary>
        public Config(IConfigurationRoot root)
        {
            _root = root;
            _deviceSerial = GetSerialNumber();
            _primaryInterface = null;
            _storage = new ConfigStorage(Path.Combine(ApplicationPath, "ConfigStorage.json"));
            _storage.LoadSettings(_deviceSerial);
        }

        /// <summary>
        /// Gets our device serial number from the primary interface's MAC addess.
        /// Maybe we'll do this different later..?
        /// </summary>
        private string GetSerialNumber()
        {
            string serial = "000000000000";
            try
            {
                NetworkInterface ni = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(i => i.NetworkInterfaceType == NetworkInterfaceType.Ethernet || i.NetworkInterfaceType == NetworkInterfaceType.GigabitEthernet)
                    .Where(i => InterfaceNames.Contains(i.Name.ToLower()))
                    .FirstOrDefault();

                if (ni != null)
                    serial = String.Join("", ni.GetPhysicalAddress()
                        .GetAddressBytes()
                        .Select(b => b.ToString("X2")))
                        .ToLower();
            }
            catch
            {
            }
            return serial;
        }

        /// <summary>
        /// Returns Windows/OSX/Linux/Unknown based on OS properties.
        /// </summary>
        public string GetOS()
        {
            try
            {
                if (IsWindows)
                    return "Windows";
                else if (IsOSX)
                    return "OSX";
                else if (IsLinux)
                    return "Linux";
            }
            catch
            {
            }
            return "Unknown";
        }

        /// <summary>
        /// Returns the amount of memory used by this process, in MB.
        /// </summary>
        public int GetMegabytesUsedByProcess()
        {
            int mb = 0;
            try
            {
                Process currentProcess = Process.GetCurrentProcess();
                long bytes = currentProcess.WorkingSet64;
                mb = (int)(bytes / 1048576);
            }
            catch
            {
            }
            return mb;
        }


        /// <summary>
        /// Stores settings that can be changed at runtime.  Loads and saves to disk.
        /// </summary>
        private class ConfigStorage
        {
            public string FilePath { get; }
            public string DeviceName { get; set; }

            public ConfigStorage(string filePath)
            {
                FilePath = filePath;
            }

            public void LoadSettings(string serial)
            {
                try
                {
                    string json = File.ReadAllText(FilePath);
                    dynamic data = JsonSerialization.Deserialize(json);
                    DeviceName = (string)data.deviceName;
                }
                catch
                {
                }

                if (String.IsNullOrWhiteSpace(DeviceName))
                {
                    DeviceName = serial.ToUpper();
                    SaveSettings();
                }
            }

            public void SaveSettings()
            {
                StringBuilder sb = new StringBuilder();
                using (SimpleJsonWriter writer = new SimpleJsonWriter(sb))
                {
                    writer.WriteStartObject();
                    writer.WritePropertyValue("deviceName", DeviceName);
                    writer.WriteEndObject();
                }
                File.WriteAllText(FilePath, sb.ToString(), Encoding.UTF8);
            }
        }

    }
}
