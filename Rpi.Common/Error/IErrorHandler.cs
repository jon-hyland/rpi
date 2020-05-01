using System;

namespace Rpi.Common.Error
{
    public interface IErrorHandler
    {
        void LogError(Exception ex);
    }
}
