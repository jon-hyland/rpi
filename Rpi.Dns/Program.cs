using Rpi.Common.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace Rpi.Dns
{
    public static class Program
    {
        //eval
        public static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        public static string ApplicationFolder => Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
        public static string ResourcesFolder => Path.Combine(ApplicationFolder, "Resources");        
        public static string HomeFolder => !IsWindows ? "/home/pi/" : @"C:\Temp\";
        public static string BackupFolder => Path.Combine(HomeFolder, "backup");
        public static string EntriesFile => Path.Combine(HomeFolder, "entries.json");
        public static string DnsmasqFile => !IsWindows ? "/etc/dnsmasq.conf" : Path.Combine(HomeFolder, "dnsmasq.conf");
        public static string DnsmasqTemplateFile => Path.Combine(ResourcesFolder, "dnsmasq.conf");
        public static string HostsFile => !IsWindows ? "/etc/hosts" : Path.Combine(HomeFolder, "hosts");
        public static string HostsTemplateFile => Path.Combine(ResourcesFolder, "hosts");

        //private
        private static List<Entry> _entries;

        /// <summary>
        /// Main entry point.
        /// </summary>
        static void Main(string[] args)
        {
            try
            {
                //header
                Version version = typeof(Program).Assembly.GetName().Version;
                Console.WriteLine($"DNS/DHCP Config Tool For Dnsmasq on Raspbian 10 (Buster) - v{version.Major}.{version.Minor}.{version.Build}");

                //load config
                _entries = LoadEntriesFile();

                //loop
                while (true)
                {
                    //write entries and menu
                    WriteMenu();

                    //get input
                    string input = Console.ReadLine().ToUpper().Trim();
                    Console.WriteLine();

                    //switch
                    bool quit = false;
                    switch (input)
                    {
                        case "A":
                            AddEntry();
                            break;
                        case "D":
                            DeleteEntry();
                            break;
                        case "S":
                            SaveEntries();
                            quit = true;
                            break;
                        case "Q":
                            quit = true;
                            break;
                        default:
                            Console.WriteLine("Invalid response");
                            break;
                    }

                    //quit?
                    if (quit)
                    {
                        if (input == "S")
                            Console.WriteLine();
                        Console.WriteLine("Program has ended.. remember to reboot device or restart services");
                        Console.WriteLine();
                        return;
                    }
                }               
            }
            catch (Exception ex)
            {
                WriteError(ex);
            }
        }

        /// <summary>
        /// Loads entries file.
        /// </summary>
        private static List<Entry> LoadEntriesFile()
        {
            try
            {
                if (!File.Exists(EntriesFile))
                {
                    Console.WriteLine($"Entries file {EntriesFile} does not exist");
                    Console.Write("Continue and create a new one? [Y/N] : ");
                    string input = Console.ReadLine().Trim().ToUpper();
                    if (input != "Y")
                        throw new Exception("Quitting program");
                    return new List<Entry>();
                }

                Console.WriteLine($"Loading file {EntriesFile}");
                string json = File.ReadAllText(EntriesFile);
                dynamic data = JsonSerialization.Deserialize(json);

                List<Entry> entries = new List<Entry>();
                foreach (dynamic e in data.entries)
                {
                    string ip = (string)e.ip;
                    string host = (string)e.host;
                    string mac = (string)e.mac;
                    entries.Add(new Entry(ip, host, mac));
                }
                entries = entries.OrderBy(e => e.SortIP).ToList();
                return entries;
            }
            catch (Exception ex)
            {
                throw new Exception($"Unable to load file {EntriesFile}", ex);
            }
        }

        /// <summary>
        /// Writes entries and main menu prompt.
        /// </summary>
        private static void WriteMenu()
        {
            Console.WriteLine();
            Console.WriteLine("ENTRIES:");
            Console.WriteLine();
            for (int i = 0; i < _entries.Count; i++)
                Console.WriteLine($" {(i < 9 ? " " : "")}[{i + 1}]  {_entries[i].IP,-16} {_entries[i].Host,-16} {_entries[i].Mac}");
            Console.WriteLine();
            Console.Write("[A]dd, [D]elete, [S]ave, [Q]uit : ");
        }

        /// <summary>
        /// Writes error (recursively) to console.
        /// </summary>
        private static void WriteError(Exception ex, int level = 0)
        {
            if (level == 0)
                Console.WriteLine($"ERROR: {ex}");
            else
                Console.WriteLine($"INNER: {ex}");

            if (ex.InnerException != null)
                WriteError(ex.InnerException, level + 1);
        }

        /// <summary>
        /// Adds a new entry.
        /// </summary>
        private static void AddEntry()
        {
            Console.WriteLine("Enter details.  Leave MAC empty if DNS entry only (no DHCP).");
            Console.Write("  IP: ");
            string ip = Entry.ValidateIP(Console.ReadLine());
            Console.Write("  Host: ");
            string host = Entry.ValidateHost(Console.ReadLine());
            Console.Write("  Mac: ");
            string mac = Entry.ValidateMac(Console.ReadLine());
            Entry entry = new Entry(ip, host, mac);
            if (_entries.Where(e => e.Host.Equals(entry.Host)).Any())
            {
                Console.Write("Host is duplicate.  Are you sure? [Y/N] : ");
                string input = Console.ReadLine().Trim().ToUpper();
                if (input != "Y")
                    return;
            }
            if (_entries.Where(e => e.IP.Equals(entry.IP)).Any())
            {
                Console.Write("IP is duplicate.  Are you sure? [Y/N] : ");
                string input = Console.ReadLine().Trim().ToUpper();
                if (input != "Y")
                    return;
            }
            if (_entries.Where(e => e.Mac.Equals(entry.Mac)).Any())
            {
                Console.Write("Mac is duplicate.  Are you sure? [Y/N] : ");
                string input = Console.ReadLine().Trim().ToUpper();
                if (input != "Y")
                    return;
            }
            _entries.Add(entry);
            _entries = _entries.OrderBy(e => e.SortIP).ToList();
        }

        /// <summary>
        /// Deletes an existing entry.
        /// </summary>
        private static void DeleteEntry()
        {
            if (_entries.Count == 0)
            {
                Console.WriteLine("Nothing to delete");
                return;
            }
            Console.Write($"Enter number to delete [{1}-{_entries.Count}] : ");
            int index = Int32.Parse(Console.ReadLine().Trim());
            if ((index < 1) || (index > _entries.Count))
            {
                Console.WriteLine("Invalid index");
                return;
            }
            Entry entry = _entries[index - 1];
            Console.Write($"Delete entry with host '{entry.Host}'? [Y/N] : ");
            string input = Console.ReadLine().Trim().ToUpper();
            if (input != "Y")
                return;
            _entries.Remove(entry);
        }

        /// <summary>
        /// Prompts and saves entries to file.
        /// </summary>
        private static void SaveEntries()
        {
            try
            {
                Console.Write("Save entries to disk? [Y/N] : ");
                string input = Console.ReadLine().Trim().ToUpper();
                if (input != "Y")
                    return;                
                BackupFiles();
                WriteEntriesFile();
                WriteDnsmasqFile();
                WriteHostsFile();
                Console.WriteLine("Configuration files written successfully");
            }
            catch (Exception ex)
            {
                throw new Exception("Error saving data", ex);
            }
        }

        /// <summary>
        /// Makes a backup of configuration files.
        /// </summary>
        private static void BackupFiles()
        {
            //create backup folder
            DateTime now = DateTime.Now;
            if (!Directory.Exists(BackupFolder))
            {
                Console.WriteLine("Creating backup folder");
                Directory.CreateDirectory(BackupFolder);
            }

            //entries
            if (File.Exists(EntriesFile))
            {
                string backupFile = Path.Combine(BackupFolder, now.ToString("yyyyMMdd_HHmmss") + "_entries" + ".json");
                Console.WriteLine($"Backing up file to {backupFile}");
                File.Copy(EntriesFile, backupFile);
            }

            //dnsmasq
            if (File.Exists(DnsmasqFile))
            {
                string backupFile = Path.Combine(BackupFolder, now.ToString("yyyyMMdd_HHmmss") + "_dnsmasq" + ".conf");
                Console.WriteLine($"Backing up file to {backupFile}");
                File.Copy(DnsmasqFile, backupFile);
            }

            //hosts
            if (File.Exists(HostsFile))
            {
                string backupFile = Path.Combine(BackupFolder, now.ToString("yyyyMMdd_HHmmss") + "_hosts");
                Console.WriteLine($"Backing up file to {backupFile}");
                File.Copy(HostsFile, backupFile);
            }
        }

        /// <summary>
        /// Saves entries file.
        /// </summary>
        private static void WriteEntriesFile()
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                using (SimpleJsonWriter writer = new SimpleJsonWriter(sb))
                {
                    writer.WriteStartObject();
                    writer.WriteStartArray("entries");
                    foreach (Entry entry in _entries)
                    {
                        writer.WriteStartObject();
                        writer.WritePropertyValue("ip", entry.IP);
                        writer.WritePropertyValue("host", entry.Host);
                        writer.WritePropertyValue("mac", entry.Mac);
                        writer.WriteEndObject();
                    }
                    writer.WriteEndArray();
                    writer.WriteEndObject();
                }

                Console.WriteLine($"Writing file {EntriesFile}");
                File.WriteAllText(EntriesFile, sb.ToString());
            }
            catch (Exception ex)
            {
                throw new Exception($"Unable to save file {EntriesFile}", ex);
            }
        }

        /// <summary>
        /// Rewrites 'dnsmasq.conf' file.
        /// </summary>
        private static void WriteDnsmasqFile()
        {
            try
            {
                if (!File.Exists(DnsmasqTemplateFile))
                    throw new Exception($"Template file '{DnsmasqTemplateFile}' not found");
                List<string> lines = new List<string>();
                foreach (string l in File.ReadAllLines(DnsmasqTemplateFile))
                {
                    string line = l.Trim();
                    lines.Add(line);
                    if (line == "### VALUES ###")
                        break;
                }
                foreach (Entry entry in _entries)
                    if (!String.IsNullOrWhiteSpace(entry.Mac))
                        lines.Add($"dhcp-host={entry.Mac.ToLower()},{entry.Host.ToLower()},{entry.IP},1440m");
                Console.WriteLine($"Writing file {DnsmasqFile}");
                File.WriteAllLines(DnsmasqFile, lines);
            }
            catch (Exception ex)
            {
                throw new Exception($"Unable to save file {DnsmasqFile}", ex);
            }
        }

        /// <summary>
        /// Rewrites 'hosts' file.
        /// </summary>
        private static void WriteHostsFile()
        {
            try
            {
                if (!File.Exists(HostsTemplateFile))
                    throw new Exception($"Template file '{HostsTemplateFile}' not found");
                List<string> lines = new List<string>();
                foreach (string l in File.ReadAllLines(HostsTemplateFile))
                {
                    string line = l.Trim();
                    lines.Add(line);
                    if (line == "### VALUES ###")
                        break;
                }
                foreach (Entry entry in _entries)
                    lines.Add($"{entry.IP}\t{entry.Host.ToLower()}");
                Console.WriteLine($"Writing file {HostsFile}");
                File.WriteAllLines(HostsFile, lines);
            }
            catch (Exception ex)
            {
                throw new Exception($"Unable to save file {HostsFile}", ex);
            }
        }
    }

    /// <summary>
    /// Represents a DNS/DHCP entry.
    /// </summary>
    public class Entry
    {
        //public
        public string IP { get; private set; }
        public string Host { get; private set; }
        public string Mac { get; private set; }
        public Version SortIP => Version.Parse(IP);

        /// <summary>
        /// Class constructor.
        /// </summary>
        public Entry(string ip, string host, string mac)
        {
            IP = ValidateIP(ip);
            Host = ValidateHost(host);
            Mac = ValidateMac(mac);
        }

        /// <summary>
        /// Validates host.
        /// </summary>
        public static string ValidateHost(string value)
        {
            value = value.Trim().ToUpper();
            Regex r = new Regex("^(([a-zA-Z0-9]|[a-zA-Z0-9][a-zA-Z0-9\\-]*[a-zA-Z0-9])\\.)*([A-Za-z0-9]|[A-Za-z0-9][A-Za-z0-9\\-]*[A-Za-z0-9])$");
            if (!r.IsMatch(value))
                throw new Exception("Invalid host");
            return value;
        }

        /// <summary>
        /// Validates IP.
        /// </summary>
        public static string ValidateIP(string value)
        {
            value = value.Trim();
            Regex r = new Regex(@"\b(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\b");
            if (!r.IsMatch(value))
                throw new Exception("Invalid IP");
            return value;
        }

        /// <summary>
        /// Validates MAC.
        /// </summary>
        public static string ValidateMac(string value)
        {
            if (String.IsNullOrWhiteSpace(value))
                return null;
            value = value.Trim().ToUpper().Replace(" ", "").Replace(":", "").Replace("-", "");
            Regex r = new Regex("^[a-fA-F0-9]{12}$");
            if (r.IsMatch(value))
            {
                if (value.Length != 12)
                    throw new Exception("Invalid Mac");
                value = $"{value.Substring(0, 2)}:{value.Substring(2, 2)}:{value.Substring(4, 2)}:{value.Substring(6, 2)}:{value.Substring(8, 2)}:{value.Substring(10, 2)}";
                return value;
            }
            else
            {
                throw new Exception("Invalid Mac");
            }
        }
    }

}
