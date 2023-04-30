using BattleTech;
using PanicSystem.Components;
using System;

// ReSharper disable InconsistentNaming

namespace PanicSystem.Patches;

[HarmonyPatch(typeof(MechMeleeSequence))]
[HarmonyPatch("CompleteOrders")]
[HarmonyPatch(MethodType.Normal)]
[HarmonyPatch(new Type[] { })]
public static class MechMeleeSequence_CompleteOrders
{
    public static void Postfix(MechMeleeSequence __instance)
    {
        TurnDamageTracker.hintAttackComplete("MechMeleeSequence:CompleteOrders");
    }
}
