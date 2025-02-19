﻿using Rpi.Common.Error;
using Rpi.Common.Output;
using Rpi.Gpio.Output;
using System;
using System.Threading;

namespace Rpi.Gpio.Error
{
    /// <summary>
    /// Primary error handling class for services.
    /// </summary>
    public class ErrorHandler : IErrorHandler
    {
        //private
        private readonly IErrorLogger[] _errorLoggers;
        private readonly ILogger _logger;

        /// <summary>
        /// Class constructor.
        /// </summary>
        public ErrorHandler(IErrorLogger errorLogger, ILogger logger)
        {
            _logger = logger;
            if (errorLogger != null)
                _errorLoggers = new IErrorLogger[] { errorLogger };
            else
                _errorLoggers = new IErrorLogger[0];
        }

        /// <summary>
        /// Class constructor.
        /// </summary>
        public ErrorHandler(IErrorLogger[] errorLoggers, ILogger logger)
        {
            _logger = logger;
            _errorLoggers = errorLoggers;
        }

        /// <summary>
        /// Logs an error to one or more configured destinations, including the internal error cache (if enabled).
        /// </summary>
        public void LogError(Exception ex)
        {
            try
            {
                if (ex is ThreadAbortException)
                {
                    throw ex;
                }
                else
                {
                    _logger?.WriteError(ex);
                    foreach (IErrorLogger logger in _errorLoggers)
                        logger.LogError(ex);
                }
            }
            catch (ThreadAbortException tae)
            {
                throw tae;
            }
            catch
            {
            }
        }
    }
}
