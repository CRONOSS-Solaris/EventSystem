using NLog;
using System;
using System.Threading.Tasks;

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

        public static async Task ErrorAsync(Logger log, string message)
        {
            await Task.Run(() => log.Error(message));
        }

        public static async Task InfoAsync(Logger log, string message)
        {
            await Task.Run(() => log.Info(message));
        }
    }
}
