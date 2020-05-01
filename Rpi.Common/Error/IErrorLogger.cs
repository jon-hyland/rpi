using System;

namespace Rpi.Common.Error
{
    public interface IErrorLogger
    {
        void LogError(Exception ex);
    }
}
