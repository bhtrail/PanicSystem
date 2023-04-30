using HBS.Logging;
using static PanicSystem.PanicSystem;

// ReSharper disable ClassNeverInstantiated.Global

namespace PanicSystem;

public class Logger
{
    private static ILog logger; 


    public static void Init()
    {
        logger = HBS.Logging.Logger.GetLogger("PanicSystem");
    }

    public static void LogReport(object line)
    {
        if (modSettings.CombatLog)
        {
            logger?.Log(line);
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
            logger?.LogDebug(input);
        }
    }
}
