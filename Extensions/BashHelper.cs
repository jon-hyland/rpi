using Rpi.Output;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Rpi.Extensions
{
    public static class ShellHelper
    {
        /// <summary>
        /// Runs any string as a Linux bash command.
        /// </summary>
        public static string Bash(this string command, int? timeoutMs, bool writeLog)
        {
            return __Bash(command, timeoutMs, writeLog).Result;
        }

        /// <summary>
        /// Runs any string as a Linux bash command.
        /// </summary>
        private static async Task<string> __Bash(string command, int? timeoutMs, bool writeLog)
        {
            StringBuilder sb = new StringBuilder();
            try
            {
                if (writeLog)
                    Log.WriteMessage("Bash", $"Running process: {command}");
                string escapedArgs = command.Replace("\"", "\\\"");
                int exitCode;
                using (StringWriter output = new StringWriter(sb))
                {
                    exitCode = await StartProcess(
                        filename: "/bin/bash",
                        arguments: $"-c \"{escapedArgs}\"",
                        workingDirectory: null,
                        timeout: timeoutMs,
                        outputTextWriter: output,
                        errorTextWriter: output);
                }
                if (writeLog)
                {
                    Log.WriteMessage("Bash", $"Process exited with code {exitCode}");
                    Log.WriteMessage("Bash", $"Process response: {sb.ToString()}");
                }
            }
            catch (TaskCanceledException tcex)
            {
                if (writeLog)
                {
                    Exception ex = new Exception($"TaskCanceledException running '{command}' with timeout '{timeoutMs ?? 0}'", tcex);
                    Log.WriteError(ex);
                }
            }
            return sb.ToString();
        }

        #region Process Stuff

        public static async Task<int> StartProcess(string filename, string arguments, string workingDirectory = null,
            int? timeout = null, TextWriter outputTextWriter = null, TextWriter errorTextWriter = null)
        {
            using (var process = new Process()
            {
                StartInfo = new ProcessStartInfo()
                {
                    CreateNoWindow = true,
                    Arguments = arguments,
                    FileName = filename,
                    RedirectStandardOutput = outputTextWriter != null,
                    RedirectStandardError = errorTextWriter != null,
                    UseShellExecute = false,
                    WorkingDirectory = workingDirectory
                }
            })
            {
                process.Start();
                var cancellationTokenSource = timeout.HasValue ? new CancellationTokenSource(timeout.Value) : new CancellationTokenSource();

                var tasks = new List<Task>(3) { process.WaitForExitAsync(cancellationTokenSource.Token) };
                if (outputTextWriter != null)
                {
                    tasks.Add(ReadAsync(x =>
                    {
                        process.OutputDataReceived += x;
                        process.BeginOutputReadLine();
                    }, x => process.OutputDataReceived -= x, outputTextWriter, cancellationTokenSource.Token));
                }

                if (errorTextWriter != null)
                {
                    tasks.Add(ReadAsync(x =>
                    {
                        process.ErrorDataReceived += x;
                        process.BeginErrorReadLine();
                    }, x => process.ErrorDataReceived -= x, errorTextWriter, cancellationTokenSource.Token));
                }

                await Task.WhenAll(tasks);
                return process.ExitCode;
            }
        }

        public static Task ReadAsync(Action<DataReceivedEventHandler> addHandler, Action<DataReceivedEventHandler> removeHandler,
        TextWriter textWriter, CancellationToken cancellationToken = default)
        {
            var taskCompletionSource = new TaskCompletionSource<object>();

            DataReceivedEventHandler handler = null;
            handler = new DataReceivedEventHandler((sender, e) =>
            {
                if (e.Data == null)
                {
                    removeHandler(handler);
                    taskCompletionSource.TrySetResult(null);
                }
                else
                {
                    textWriter.WriteLine(e.Data);
                }
            });

            addHandler(handler);

            if (cancellationToken != default)
            {
                cancellationToken.Register(() =>
                {
                    removeHandler(handler);
                    taskCompletionSource.TrySetCanceled();
                });
            }

            return taskCompletionSource.Task;
        }

        #endregion
    }
}