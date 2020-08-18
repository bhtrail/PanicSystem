using System;
using System.IO;
using Harmony;
using HBS.Logging;
using static PanicSystem.PanicSystem;

// ReSharper disable ClassNeverInstantiated.Global

namespace PanicSystem
{
    public class Logger
    {
        private const string LOGGER_NAME = "PanicSystem";

        private static string LogFilePath => Path.Combine(modDirectory, "log.txt");

        private static ILog logger = HBS.Logging.Logger.GetLogger(LOGGER_NAME, LogLevel.Error);

        public static void LogReport(object line)
        {
            if (modSettings.CombatLog)
            {
                using (var writer = new StreamWriter(LogFilePath, true))
                {
                    writer.WriteLine($"{line}");
                }
            }
        }

        internal static void LogDebug(object input)
        {
            /*if (modSettings.CombatLog)
            {
                try
                {
                    using (var writer = new StreamWriter(LogFilePath, true))
                    {
                        writer.WriteLine($" {input ?? "null"}");
                    }
                }
                catch (Exception ) { }
            }*/

            if (modSettings.Debug)
            {
                FileLog.Log($"[PanicSystem] {input ?? "null"}");
            }
        }

        public static void LogError(object message, Exception exception)
        {
            logger.LogError(message, exception);
        }

        public static void LogError(object message)
        {
            logger.LogError(message);
        }
    }
}
