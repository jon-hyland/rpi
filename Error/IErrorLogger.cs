using System;

namespace Rpi.Error
{
    public interface IErrorLogger
    {
        void LogError(Exception ex);
    }
}
