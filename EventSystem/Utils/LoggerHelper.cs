using NLog;
using System;

namespace EventSystem.Utils
{
    public static class LoggerHelper
    {
        public static void DebugLog(Logger log, EventSystemConfig config, string message, Exception exception = null)
        {
            if (config?.DebugMode ?? false)
            {
                if (exception == null)
                {
                    log.Warn(message);
                }
                else
                {
                    log.Warn(exception, message);
                }
            }
        }
    }
}
