//using Rpi.Error;
//using Rpi.Extensions;
//using Rpi.Output;
//using Rpi.Service;
//using Rpi.Threading;
//using System;
//using System.Collections.Generic;
//using System.Diagnostics;
//using System.Linq;
//using System.Net.NetworkInformation;
//using System.Net.Sockets;

//namespace Rpi.Health
//{
//    /// <summary>
//    /// Checks every 10 seconds for a valid IP address assignment.  If not,
//    /// performs a manual DHCP release (probably not needed) followed
//    /// by a manual DHCP renew.  Will eventually aquire an IP address lease,
//    /// either from the SWAK application on the machine's internal network, OR
//    /// from a network DHCP server if connected to a standard network.
//    /// </summary>
//    public class DhcpStatus : IDisposable
//    {
//        //private
//        private readonly IErrorHandler _errorHandler;
//        private readonly Config _config;
//        private readonly ServiceStats _serviceStats;
//        private readonly SimpleTimer _timer;
//        private readonly HashSet<string> _interfaceNames;
//        private bool _verboseLogs;

//        /// <summary>
//        /// Class constructor.
//        /// </summary>
//        public DhcpStatus(IErrorHandler errorHandler, Config config, ServiceStats serviceStats)
//        {
//            Log.WriteMessage("DhcpStatus", $"Creating DHCP status monitor..");
//            _errorHandler = errorHandler;
//            _config = config;
//            _serviceStats = serviceStats;
//            _timer = new SimpleTimer(Timer_Callback, 10000);
//            _interfaceNames = new HashSet<string>(_config.InterfaceNames);
//            _verboseLogs = config.VerboseDhcpLogging;
//        }

//        /// <summary>
//        /// Fired on timer callback.
//        /// </summary>
//        private void Timer_Callback()
//        {
//            try
//            {
//                CheckForDhcpProblems();
//            }
//            catch (Exception ex)
//            {
//                _errorHandler?.LogError(ex);
//            }
//        }

//        /// <summary>
//        /// Checks for DHCP problems (interface not getting a correct IP) and
//        /// forces Linux DHCP client (dhclient) 
//        /// </summary>
//        private void CheckForDhcpProblems()
//        {
//            try
//            {
//                //find default interface
//                List<NetworkInterface> interfaces = NetworkInterface.GetAllNetworkInterfaces()
//                    .Where(i => i.NetworkInterfaceType == NetworkInterfaceType.Ethernet || i.NetworkInterfaceType == NetworkInterfaceType.GigabitEthernet)
//                    .ToList();
//                NetworkInterface ni = null;
//                foreach (NetworkInterface i in interfaces)
//                {
//                    foreach (string partial in _interfaceNames)
//                    {
//                        if (i.Name.Contains(partial, StringComparison.InvariantCultureIgnoreCase))
//                        {
//                            ni = i;
//                            break;
//                        }
//                    }
//                    if (ni != null)
//                        break;
//                }

//                //no matching interface
//                if (ni == null)
//                {
//                    Log.WriteMessage("Dhcp", $"No interface found with a name containing values: [{String.Join(", ", _interfaceNames)}]");
//                    return;
//                }

//                //get ip address
//                string ip = ni.GetIPProperties().UnicastAddresses
//                    .Select(ua => ua.Address)
//                    .Where(a => a.AddressFamily == AddressFamily.InterNetwork)
//                    .FirstOrDefault()?.ToString();

//                //no ip or APIPA ip?
//                if ((String.IsNullOrWhiteSpace(ip)) || (ip.StartsWith("169.")))
//                {
//                    //message
//                    if (_verboseLogs)
//                        Log.WriteMessage("Dhcp", $"Device does not yet have a valid IP address ({ip ?? "NULL"})..");

//                    //release
//                    Release(ni.Name);

//                    //renew
//                    Renew(ni.Name);
//                }
//                else
//                {
//                    //message
//                    if (_verboseLogs)
//                        Log.WriteMessage("Dhcp", $"Device has valid IP address ({ip ?? "NULL"})..");
//                }                
//            }
//            catch (Exception ex)
//            {
//                _errorHandler?.LogError(ex);
//            }
//        }

//        /// <summary>
//        /// Perform a manual DHCP release.
//        /// </summary>
//        private void Release(string name)
//        {
//            if (!_config.IsLinux)
//                return;

//            Stopwatch sw = Stopwatch.StartNew();
//            try
//            {
//                if (_verboseLogs)
//                    Log.WriteMessage("Dhcp", "Releasing DHCP assignment..");
//                string response = $"dhclient -d -r {name}".Bash(4000, _verboseLogs);
//                if (_verboseLogs)
//                    Log.WriteMessage("Dhcp", "Release response: " + response);
//            }
//            catch (Exception ex)
//            {
//                if (_verboseLogs)
//                    _errorHandler?.LogError(ex);
//            }
//            finally
//            {
//                sw.Stop();
//                _serviceStats.LogOperation("DhcpStatus.Release", sw.ElapsedMilliseconds);
//            }
//        }

//        /// <summary>
//        /// Perform a manual DHCP renew.
//        /// </summary>
//        private void Renew(string name)
//        {
//            if (!_config.IsLinux)
//                return;

//            Stopwatch sw = Stopwatch.StartNew();
//            try
//            {
//                if (_verboseLogs)
//                    Log.WriteMessage("Dhcp", "Renew DHCP assignment..");
//                string response = $"dhclient -1 -d {name}".Bash(4000, _verboseLogs);
//                if (_verboseLogs)
//                    Log.WriteMessage("Dhcp", "Renew response: " + response);
//            }
//            catch (Exception ex)
//            {
//                if (_verboseLogs)
//                    _errorHandler?.LogError(ex);
//            }
//            finally
//            {
//                sw.Stop();
//                _serviceStats.LogOperation("DhcpStatus.Release", sw.ElapsedMilliseconds);
//            }
//        }

//        /// <summary>
//        /// Dispose of resources.
//        /// </summary>
//        public void Dispose()
//        {
//            _timer?.Stop();
//            _timer?.Dispose();
//        }

//    }
//}
