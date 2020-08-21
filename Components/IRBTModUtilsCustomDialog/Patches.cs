using System;
using System.IO;
using BattleTech;
using BattleTech.UI;
using Harmony;
using UnityEngine;
// ReSharper disable All

// thank you Frosty IRBTModUtils CustomDialog
// https://github.com/IceRaptor/IRBTModUtils
namespace PanicSystem.Components.IRBTModUtilsCustomDialog {

    // Initialize shared elements (CombatGameState, etc)
    [HarmonyPatch(typeof(CombatGameState), "_Init")]
    [HarmonyPatch(new Type[] { typeof(GameInstance), typeof(Contract), typeof(string) })]
    public static class CombatGameState__Init
    {
        public static void Postfix(CombatGameState __instance)
        {

            ModState.Combat = __instance;

            // Load any dialogue portraits at startup
            if (ModState.Portraits.Count == 0)
            {
                foreach (string portraitPath in PanicSystem.modSettings.Dialogue.Portraits)
                {
                    string path = Utilities.PathUtils.AppendPath(EmotePortrait.SpriteBasePath, portraitPath, appendForwardSlash: false);
                    if (File.Exists(path))
                    {
                        Sprite sprite = Utilities.ImageUtils.LoadSprite(path);
                        ModState.Portraits.Add(portraitPath, sprite);
                    }
                }
            }
        }
    }

    // Teardown shared elements to prevent NREs
    [HarmonyPatch(typeof(CombatGameState), "OnCombatGameDestroyed")]
    public static class CombatGameState_OnCombatGameDestroyed
    {
        public static void Prefix()
        {
            ModState.Combat = null;
        }
    }

    // Register listeners for our events, using the CombatHUD hook
    [HarmonyPatch(typeof(CombatHUD), "SubscribeToMessages")]
    public static class CombatHUD_SubscribeToMessages {
        public static void Postfix(CombatHUD __instance, bool shouldAdd) {
            if (__instance != null) {
                __instance.Combat.MessageCenter.Subscribe(
                    (MessageCenterMessageType)MessageTypes.OnCustomDialog, new ReceiveMessageCenterMessage(Coordinator.OnCustomDialogMessage), shouldAdd);
            }
        }
    }

    // Initialize shared elements (CombatGameState, etc)
    [HarmonyPatch(typeof(CombatHUD), "Init")]
    [HarmonyPatch(new Type[] {  typeof(CombatGameState) })]
    public static class CombatHUD_Init {
        public static void Postfix(CombatHUD __instance, CombatGameState Combat) {
            Coordinator.OnCombatHUDInit(Combat, __instance);
        }
    }

    // Teardown shared elements to prevent NREs
    [HarmonyPatch(typeof(CombatHUD), "OnCombatGameDestroyed")]
    public static class CombatHUD_OnCombatGameDestroyed {
        public static void Prefix() {
            Coordinator.OnCombatGameDestroyed();
        }
    }
}
