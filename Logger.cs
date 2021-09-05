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
    internal class Logger
    {
        private static StreamWriter logStreamWriter;
        public Logger(string modDir, string fileName)
        {
            string filePath = Path.Combine(modDir, $"{fileName}.log");
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            logStreamWriter = File.AppendText(filePath);
            logStreamWriter.AutoFlush = true;
        }

        public void LogReport(object line)
        {
            if (modSettings.CombatLog)
            {
                try
                {
                    string ts = DateTime.Now.ToString("s", System.Globalization.CultureInfo.InvariantCulture);
                    logStreamWriter.WriteLine($"REPORT {ts}: {line ?? "null"}");
                }
                catch (Exception exception)
                {
                    LogError(exception);
                }
            }
        }

        public static void LogError(object message)
        {
            Logger.LogError(message);
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
                            modLog.LogReport("ACTOR LOG HISTORY CLEARED");
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

                    string input =
                        $"ACTOR {actor.GUID}-{actor.DisplayName}-{actor.DisplayName} / {pilotable} / {can_eject} / {can_eject_tag} / {can_eject_stat}";
                    if (actor.IsPilotable && actor.GetPilot() != null && actor.GetPilot().CanEject == false)
                    {
                        input = $"{input}\r\n{pilotdesc}\r\n{actordesc}";
                    }

                    logStreamWriter.WriteLine($" {input ?? "null"}");
                }
            }
            catch (Exception exception)
            {
                LogError(exception);
            }
        }
    }
}
