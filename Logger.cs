using System;
using System.Collections.Generic;
using System.IO;
using BattleTech;
using Harmony;
using HBS.Logging;
using static PanicSystem.PanicSystem;

// ReSharper disable ClassNeverInstantiated.Global

namespace PanicSystem
{
    public class Logger
    {
        private const string LOGGER_NAME = "PanicSystem";

        public static string LogFilePath => Path.Combine(modDirectory, "PanicSystem.log");

        private static ILog logger = HBS.Logging.Logger.GetLogger(LOGGER_NAME, LogLevel.Error);

        public static void LogReport(object line)
        {
            if (modSettings.CombatLog)
            {
                try
                {
                    using (var writer = new StreamWriter(LogFilePath, true))
                    {
                        writer.WriteLine($"{line}");
                    }
                }
                catch (Exception) { }
            }
        }

        internal static void LogDebug(object input)
        {
            if (modSettings.CombatLog)
            {
                try
                {

                    using (var writer = new StreamWriter(LogFilePath, true))
                    {
                        writer.WriteLine($" {input ?? "null"}");
                    }
                }
                catch (Exception ) { }
            }

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

        private static List<string> loggedactors = new List<string>();
        internal static void LogActor(AbstractActor actor,bool force)
        {
            try
            {
                if (modSettings.CombatLog)
                {

                    if (!force && loggedactors.Contains(actor.GUID))
                    {
                        return;
                    }
                    else
                    {
                        loggedactors.Add(actor.GUID);
                        if (loggedactors.Count > 100)
                        {
                            loggedactors.Clear();
                            LogDebug("ACTOR LOG HISTORY CLEARED");
                        }
                    }

                    string actordesc = "";
                    string pilotable = actor.IsPilotable ? "pilotable" : "non-pilotable";
                    string pilotdesc = "NA";
                    string can_eject = "NA";
                    string can_eject_tag = "NA";
                    string can_eject_stat = "NA";
                    if (actor.IsPilotable && actor.GetPilot() != null)
                    {
                        can_eject_tag = "No pilot_cannot_eject tag";
                        can_eject_stat = "No CanEject stat";
                        Pilot p = actor.GetPilot();
                        pilotdesc = p.ToPilotDef(true).ToJSON();
                        can_eject = p.CanEject ? "true" : "false";
                        if (!p.CanEject)
                        {
                            if (p.pilotDef.PilotTags.Contains("pilot_cannot_eject"))
                            {
                                can_eject_tag = "Has pilot_cannot_eject tag";
                            }
                            if (p.StatCollection.GetValue<bool>("CanEject"))
                            {
                                can_eject_stat = "Has CanEject stat";
                            }
                            List<MechComponent> cs = actor.allComponents;
                            foreach (MechComponent c in cs)
                            {
                                actordesc = $"{actordesc}\r\n{c.ToMechComponentDef().ToJSON()}";
                            }
                        }
                    }

                    string input = $"ACTOR {actor.GUID}-{actor.DisplayName}-{actor.DisplayName} / {pilotable} / {can_eject} / {can_eject_tag} / {can_eject_stat}";
                    if (actor.IsPilotable && actor.GetPilot() != null && actor.GetPilot().CanEject == false)
                    {
                        input = $"{input}\r\n{pilotdesc}\r\n{actordesc}";
                    }
                    using (var writer = new StreamWriter(LogFilePath, true))
                    {
                        writer.WriteLine($" {input ?? "null"}");
                    }

                }
            }
            catch (Exception) { }
        }
    }
}
