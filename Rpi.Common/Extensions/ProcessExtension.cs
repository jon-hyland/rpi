using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Rpi.Common.Extensions
{
    public static class ProcessExtension
    {
        public static Task WaitForExitAsync(this Process process, CancellationToken cancellationToken = default)
        {
            process.EnableRaisingEvents = true;
            var taskCompletionSource = new TaskCompletionSource<object>();

            void handler(object sender, EventArgs args)
            {
                process.Exited -= handler;
                taskCompletionSource.TrySetResult(null);
            }

            process.Exited += handler;

            if (cancellationToken != default)
            {
                cancellationToken.Register(() =>
                {
                    process.Exited -= handler;
                    taskCompletionSource.TrySetCanceled();
                });
            }

            return taskCompletionSource.Task;
        }

    
    }
}
