using System;

namespace Rpi.Common.Output
{
    public interface ILogger
    {
        void WriteMessage(string header, string message);
        void WriteError(Exception ex);
    }
}
