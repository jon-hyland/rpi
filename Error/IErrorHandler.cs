using System;

namespace Rpi.Error
{
    public interface IErrorHandler
    {
        void LogError(Exception ex);
    }
}
