using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Harmony;
using Newtonsoft.Json;
using PanicSystem.Components.IRBTModUtilsCustomDialog;
using static PanicSystem.Logger;

// ReSharper disable InconsistentNaming

// HUGE thanks to RealityMachina and mpstark for their work, outstanding.
namespace PanicSystem
{
    public static class PanicSystem
    {
        internal static Settings modSettings = new Settings();
        internal static string activeJsonPath; //store current tracker here
        internal static string storageJsonPath; //store our meta trackers here
        internal static string modDirectory;
        internal static List<string> ejectPhraseList = new List<string>();
        internal static HarmonyInstance harmony;

        public static void Init(string modDir, string settings)
        {
            try
            {

                harmony = HarmonyInstance.Create("com.BattleTech.PanicSystem");
                modDirectory = modDir;
                if (File.Exists(LogFilePath))
                {
                    File.Delete(LogFilePath);
                }
                activeJsonPath = Path.Combine(modDir, "PanicSystem.json");
                storageJsonPath = Path.Combine(modDir, "PanicSystemStorage.json");
                try
                {
                    modSettings = JsonConvert.DeserializeObject<Settings>(settings);
                }
                catch (Exception ex)
                {
                    LogDebug(ex);
                    modSettings = new Settings();
                }

                // Determine the BT directory, and read the HBS callsigns to use in CustomDialogs
                //   See https://github.com/IceRaptor/IRBTModUtils for original source.
                string fileName = Process.GetCurrentProcess().MainModule.FileName;
                string btDir = Path.GetDirectoryName(fileName);
                if (Coordinator.CallSigns == null)
                {
                    string filePath = Path.Combine(btDir, modSettings.Dialogue.CallsignsPath);
                    try
                    {
                        Coordinator.CallSigns = File.ReadAllLines(filePath).ToList();
                    }
                    catch (Exception e)
                    {
                        LogDebug("Failed to read callsigns from BT directory!");
                        LogDebug(e);
                        Coordinator.CallSigns = new List<string> { "Alpha", "Beta", "Gamma" };
                    }
                }

                harmony.PatchAll();
                Helpers.SetupEjectPhrases(modDir);
                LogDebug($"Initializing PanicSystem - Version {typeof(Settings).Assembly.GetName().Version}");
            }
            catch (Exception ex)
            {
                LogDebug(ex);
                throw ex;
            }
        }
    }
}
