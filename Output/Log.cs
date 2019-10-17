using Rpi.Threading;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Rpi.Output
{
    /// <summary>
    /// Logs stuff to file.
    /// </summary>
    public static class Log
    {
        //private
        private static string _folder = null;
        private static string _version = null;
        private static SimpleTimer _timer = null;
        private static readonly List<string> _fileBuffer = new List<string>();
        private static readonly object _bufferLock = new object();
        private static readonly object _fileLock = new object();

        /// <summary>
        /// Initializes static class.
        /// </summary>
        public static void Initialize(string folder, string version)
        {
            _folder = folder;
            _version = version;
            if (!Directory.Exists(_folder))
                Directory.CreateDirectory(_folder);
            _timer = new SimpleTimer(Timer_Callback, 30);
        }

        /// <summary>
        /// Writes a message to the log.
        /// </summary>
        public static void WriteMessage(string header, string message)
        {
            try
            {
                message = message.Trim()
                    .Replace("\r\n", " ")
                    .Replace("\r", " ")
                    .Replace("\n", " ")
                    .Replace("\t", " ")
                    .Trim();
                while (message.Contains("  "))
                    message = message.Replace("  ", " ");
                Console.WriteLine(message);

                string line = $"{DateTime.Now.ToString()}\t{"##VERSION##"}\tMessage\t{header}\t{message}";
                WriteLine(line, false);
            }
            catch
            {
            }
        }

        /// <summary>
        /// Writes an error to the log.
        /// </summary>
        public static void WriteError(Exception ex)
        {
            try
            {
                string message = ex.ToString()
                    .Replace("\r\n", " ")
                    .Replace("\r", " ")
                    .Replace("\n", " ")
                    .Replace("\t", " ")
                    .Trim();
                while (message.Contains("  "))
                    message = message.Replace("  ", " ");
                Console.WriteLine(message);

                string line = $"{DateTime.Now.ToString()}\t{_version}\tError\t{ex.GetType()}\t{message}";
                WriteLine(line, true);
            }
            catch
            {
            }
        }

        /// <summary>
        /// Writes a line to the log file.  Optionally, writes the buffer immediately.
        /// </summary>
        private static void WriteLine(string line, bool writeNow)
        {
            lock (_bufferLock)
            {
                while (_fileBuffer.Count > 100000)
                    _fileBuffer.RemoveAt(0);
                _fileBuffer.Add(line);
            }
            if (writeNow)
                WriteBufferNow();
        }

        /// <summary>
        /// Writes the buffer to file.
        /// </summary>
        private static void WriteBufferNow()
        {
            try
            {
                List<string> copy = null;
                lock (_bufferLock)
                {
                    if (_fileBuffer.Count > 0)
                    {
                        copy = new List<string>(_fileBuffer);
                        _fileBuffer.Clear();
                    }
                }

                if (copy != null)
                {
                    lock (_fileLock)
                    {
                        string file = Path.Combine(_folder, $"rpi.{DateTime.Now.ToString("yyyyMMdd")}.log");
                        StringBuilder sb = new StringBuilder();
                        foreach (string line in copy)
                            sb.AppendLine(line.Replace("##VERSION##", _version));
                        File.AppendAllText(file, sb.ToString());
                    }
                }
            }
            catch
            {
            }
        }

        /// <summary>
        /// Fired by timer.
        /// </summary>
        private static void Timer_Callback()
        {
            WriteBufferNow();
        }

        /// <summary>
        /// Scans through log files (most recent backwards) reading most recent log
        /// entries until it has enough to fill the request.
        /// </summary>
        public static List<string> GetLog(int maxLines = 1000, double minutes = 1440 * 7)
        {
            List<List<string>> lines = new List<List<string>>();
            int countLines() => lines.SelectMany(list => list).Count();
            DateTime earliest = DateTime.Now.Subtract(TimeSpan.FromMinutes(minutes));
            lock (_fileLock)
            {
                string[] files = Directory.GetFiles(_folder, "rpi.*.log")
                    .OrderByDescending(f => f)
                    .ToArray();

                foreach (string file in files)
                {
                    long remaining = maxLines - countLines();
                    long count = CountFileLines(file);
                    long skip = count > remaining ? count - remaining : 0;
                    DateTime latestInternalTimestamp = ParseFileDate(file).AddDays(1);

                    if (latestInternalTimestamp < earliest)
                        continue;

                    long skipped = 0;
                    using (StreamReader reader = new StreamReader(file))
                    {
                        List<string> fileLines = new List<string>();
                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            if (skipped < skip)
                            {
                                skipped++;
                                continue;
                            }

                            DateTime timestamp = ParseLineTimestamp(line);
                            if (timestamp < earliest)
                                continue;

                            fileLines.Add(line);
                        }
                        lines.Add(fileLines);
                    }

                    if (countLines() >= maxLines)
                        break;
                }

                if (countLines() > maxLines)
                    throw new Exception("Too many lines");
            }
            return ((IEnumerable<List<string>>)lines)
                .Reverse()
                .SelectMany(list => list)
                .ToList();
        }

        /// <summary>
        /// Counts number of lines in a file.
        /// </summary>
        private static long CountFileLines(string file)
        {
            long count = 0;
            using (StreamReader reader = new StreamReader(file))
            {
                while (reader.ReadLine() != null)
                    count++;
            }
            return count;
        }

        /// <summary>
        /// Parses the date portion of file name.
        /// </summary>
        private static DateTime ParseFileDate(string file)
        {
            try
            {
                string[] split = file.Split(new char[] { '.' }, StringSplitOptions.None);
                return DateTime.ParseExact(split[2], "yyyyMMdd", CultureInfo.InvariantCulture);
            }
            catch
            {
                return DateTime.MinValue;
            }
        }

        /// <summary>
        /// Parses the date portion of a log entry line.
        /// </summary>
        private static DateTime ParseLineTimestamp(string line)
        {
            try
            {
                string[] split = line.Split(new char[] { '\t' }, StringSplitOptions.None);
                DateTime timestamp = DateTime.Parse(split[0]);
                return timestamp;
            }
            catch
            {
                return DateTime.MinValue;
            }
        }

      


    }
}
