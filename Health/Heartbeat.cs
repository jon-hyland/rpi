using Rpi.Error;
using Rpi.Json;
using Rpi.Output;
using Rpi.Service;
using Rpi.Threading;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

namespace Rpi.Health
{
    /// <summary>
    /// Collects information and sends UDP broadcast packet (heartbeat)
    /// with statistics every second.
    /// </summary>
    public class Heartbeat : IDisposable
    {
        //private
        private readonly IErrorHandler _errorHandler;
        private readonly Config _config;
        private readonly ServiceStats _serviceStats;
        private readonly ServiceState _serviceState;
        private readonly SimpleTimer _timer;
        private readonly UdpClient _udpClient;
        private readonly IPEndPoint _endPoint;
        private readonly HashSet<string> _interfaceNames;

        /// <summary>
        /// Class constructor.
        /// </summary>
        public Heartbeat(IErrorHandler errorHandler, Config config, ServiceStats serviceStats, ServiceState serviceState)
        {
            Log.WriteMessage("Heartbeat", $"Creating heatbeat service..");
            _errorHandler = errorHandler;
            _config = config;
            _serviceStats = serviceStats;
            _serviceState = serviceState;
            _timer = new SimpleTimer(Timer_Callback, 1000, false, true, true);
            _udpClient = new UdpClient();
            _endPoint = new IPEndPoint(IPAddress.Broadcast, 5002);
            _interfaceNames = new HashSet<string>(_config.InterfaceNames);
        }

        /// <summary>
        /// Starts the heartbeat timer.
        /// </summary>
        public void Start()
        {
            Log.WriteMessage("Heartbeat", "Starting heatbeat service..");
            _timer.Start();
        }

        /// <summary>
        /// Stops the heartbeat timer.
        /// </summary>
        public void Stop()
        {
            Log.WriteMessage("Heartbeat", "Stopping heatbeat service..");
            _timer.Stop();
        }

        /// <summary>
        /// Fired on timer callback.
        /// </summary>
        private void Timer_Callback()
        {
            try
            {
                PacketData data = CollectData();
                SendData(data);
            }
            catch (Exception ex)
            {
                _errorHandler?.LogError(ex);
            }
        }

        /// <summary>
        /// Collects system information.
        /// </summary>
        private PacketData CollectData()
        {
            Stopwatch sw = Stopwatch.StartNew();
            try
            {
                string serial = _config.DeviceSerial;
                string name = _config.DeviceName;
                string version = _config.ServiceVersion;
                int httpPort = _config.ListenPort;
                TimeSpan runningTime = Main.RunningTime;
                string serviceState = _serviceState.GetState().ToString();

                List<NetworkInterface> networkInterfaces = NetworkInterface.GetAllNetworkInterfaces()
                   .Where(i => i.NetworkInterfaceType == NetworkInterfaceType.Ethernet || i.NetworkInterfaceType == NetworkInterfaceType.GigabitEthernet)
                   .Where(i => _interfaceNames.Contains(i.Name.ToLower()))
                   .ToList();

                List<Interface> interfaces = new List<Interface>();
                foreach (NetworkInterface ni in networkInterfaces)
                {
                    IPAddress ip = ni.GetIPProperties().UnicastAddresses
                        .Select(ua => ua.Address)
                        .Where(a => a.AddressFamily == AddressFamily.InterNetwork)
                        .FirstOrDefault();

                    string iname = ni.Name;
                    string iphysical = String.Join(":", ni.GetPhysicalAddress().GetAddressBytes().Select(b => b.ToString("X2")));
                    string iinternet = ip?.ToString();

                    interfaces.Add(new Interface(iname, iphysical, iinternet));
                }

                Interface primaryInterface = interfaces.FirstOrDefault();
                if ((primaryInterface != null) && ((_config.PrimaryInterface == null) || (!primaryInterface.Equals(_config.PrimaryInterface))))
                    _config.PrimaryInterface = primaryInterface;

                PacketData data = new PacketData(serial, name, version, httpPort, runningTime, serviceState, interfaces);
                return data;
            }
            finally
            {
                sw.Stop();
                _serviceStats.LogOperation("Heartbeat.CollectData", sw.ElapsedMilliseconds);
            }
        }

        /// <summary>
        /// Sends UDP broadcast packet containing system information.
        /// </summary>
        private void SendData(PacketData data)
        {
            Stopwatch sw = Stopwatch.StartNew();
            try
            {
                string json = data.ToJson();
                byte[] payload = Encoding.UTF8.GetBytes(json);
                byte[] header = new byte[] { 88, 121 };
                byte[] version = new byte[] { 1 };
                byte[] length = BitConverter.GetBytes(payload.Length);

                byte[] bytes = new byte[payload.Length + header.Length + 1 + 4];
                int pos = 0;
                Array.Copy(header, 0, bytes, pos, header.Length);
                pos += header.Length;
                Array.Copy(version, 0, bytes, pos, version.Length);
                pos += version.Length;
                Array.Copy(length, 0, bytes, pos, length.Length);
                pos += length.Length;
                Array.Copy(payload, 0, bytes, pos, payload.Length);

                _udpClient.Send(bytes, bytes.Length, _endPoint);
            }
            finally
            {
                sw.Stop();
                _serviceStats.LogOperation("Heartbeat.SendData", sw.ElapsedMilliseconds);
            }
        }

        /// <summary>
        /// Dispose of resources.
        /// </summary>
        public void Dispose()
        {
            _timer?.Stop();
            _timer?.Dispose();
            _udpClient?.Dispose();
        }

        /// <summary>
        /// Stores system information sent in heartbeat packet.
        /// </summary>
        public class PacketData
        {
            public string Serial { get; }
            public string Name { get; }
            public string Version { get; }
            public int HttpPort { get; }
            public TimeSpan RunningTime { get; }
            public string ServiceState { get; }
            public List<Interface> Interfaces { get; }
            public int Hash { get; }

            public PacketData(string serial, string name, string version, int httpPort, TimeSpan runningTime, string serviceState)
            {
                Serial = serial;
                Name = name;
                Version = version;
                HttpPort = httpPort;
                RunningTime = runningTime;
                ServiceState = serviceState;
                Interfaces = new List<Interface>();
                Hash = ($"{Serial}|{Name}|{Version}|{HttpPort}|{ServiceState}|{String.Join("|", Interfaces.Select(i => i.GetHashCode().ToString()))}").GetHashCode();
            }

            public PacketData(string serial, string name, string version, int httpPort, TimeSpan runningTime, string serviceState, List<Interface> interfaces)
            {
                Serial = serial;
                Name = name;
                Version = version;
                HttpPort = httpPort;
                RunningTime = runningTime;
                ServiceState = serviceState;
                Interfaces = interfaces;
                Hash = ($"{Serial}|{Name}|{Version}|{HttpPort}|{ServiceState}|{String.Join("|", Interfaces.Select(i => i.GetHashCode().ToString()))}").GetHashCode();
            }

            public string ToJson()
            {
                StringBuilder sb = new StringBuilder();
                using (SimpleJsonWriter writer = new SimpleJsonWriter(sb, true))
                {
                    writer.WriteStartObject();
                    writer.WritePropertyValue("serial", Serial);
                    writer.WritePropertyValue("name", Name);
                    writer.WritePropertyValue("version", Version);
                    writer.WritePropertyValue("httpPort", HttpPort);
                    writer.WritePropertyValue("runningSecs", (int)RunningTime.TotalSeconds);
                    writer.WritePropertyValue("serviceState", ServiceState);
                    writer.WriteStartArray("interfaces");
                    foreach (Interface i in Interfaces)
                    {
                        writer.WriteStartObject();
                        writer.WritePropertyValue("name", i.Name);
                        writer.WritePropertyValue("physical", i.PhysicalAddress);
                        writer.WritePropertyValue("internet", i.InternetAddress);
                        writer.WriteEndObject();
                    }
                    writer.WriteEndArray();
                    writer.WriteEndObject();
                }
                return sb.ToString();
            }

            public static PacketData FromJson(string json)
            {
                dynamic data = JsonSerialization.Deserialize(json);
                string serial = (string)data.serial;
                string name = (string)data.name;
                string version = (string)data.version;
                int httpPort = (int)data.httpPort;
                TimeSpan runningTime = TimeSpan.FromSeconds((int)data.runningSecs);
                string serviceState = (string)data.serviceState ?? "Unknown";

                List<Interface> interfaces = new List<Interface>();
                foreach (dynamic i in data.interfaces)
                {
                    string iName = (string)i.name;
                    string iPhysical = (string)i.physical;
                    string iInternat = (string)i.internet;
                    interfaces.Add(new Interface(iName, iPhysical, iInternat));
                }

                return new PacketData(serial, name, version, httpPort, runningTime, serviceState, interfaces);
            }

            public override int GetHashCode()
            {
                return Hash;
            }
        }
    }

    /// <summary>
    /// Represents a network interface.
    /// </summary>
    public class Interface
    {
        public string Name { get; }
        public string PhysicalAddress { get; }
        public string InternetAddress { get; }

        public Interface(string name, string physicalAddress, string internetAddress)
        {
            Name = name;
            PhysicalAddress = physicalAddress;
            InternetAddress = internetAddress;
        }

        public override int GetHashCode()
        {
            int hash = 17;
            hash = hash * 31 + (Name ?? String.Empty).GetHashCode();
            hash = hash * 31 + (PhysicalAddress ?? String.Empty).GetHashCode();
            hash = hash * 31 + (InternetAddress ?? String.Empty).GetHashCode();
            return hash;
        }

        public override bool Equals(object obj)
        {
            if ((obj == null) || (!(obj is Interface i)))
                return false;
            return i.GetHashCode() == GetHashCode();
        }
    }
}
