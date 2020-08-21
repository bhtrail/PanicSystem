using BattleTech;
using Harmony;
using PanicSystem.Components;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using BattleTech.Achievements;
using static PanicSystem.Logger;

// ReSharper disable InconsistentNaming

namespace PanicSystem.Patches
{
    [HarmonyPatch(typeof(CombatGameState))]
    [HarmonyPatch("OnCombatGameDestroyed")]
    [HarmonyPatch(MethodType.Normal)]
    [HarmonyPatch(new Type[] { })]
    public static class CombatGameState_OnCombatGameDestroyedMap
    {
        private static void Postfix() => TurnDamageTracker.Reset();
    }

    [HarmonyPatch(typeof(CombatGameState))]
    [HarmonyPatch("_Init")]
    [HarmonyPatch(MethodType.Normal)]
    [HarmonyPatch(new Type[] { typeof(GameInstance), typeof(Contract), typeof(string) })]
    public static class CombatGameState_Init
    {
        private static FieldInfo combatProcessors =
            typeof(BattleTechAchievmentsAPI).GetField(nameof(combatProcessors), AccessTools.all);

        private static void Postfix()
        {
            try
            {
                if (combatProcessors.GetValue(UnityGameInstance.BattleTechGame.Achievements) is AchievementProcessor[] processors)
                {
                    foreach (AchievementProcessor a in processors)
                    {
                        if (a is CombatProcessor combat)
                        {
                            DamageHandler.CombatProcessor = combat;
                            break;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                LogError(e);
            }
        }
    }
}
