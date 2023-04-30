using BattleTech;
using PanicSystem.Components;
using System;

// ReSharper disable InconsistentNaming

namespace PanicSystem.Patches;

[HarmonyPatch(typeof(ActorMovementSequence))]
[HarmonyPatch("CompleteOrders")]
[HarmonyPatch(MethodType.Normal)]
[HarmonyPatch(new Type[] { })]
public static class ActorMovementSequence_CompleteOrders
{
    public static void Postfix(ActorMovementSequence __instance)
    {
            TurnDamageTracker.hintAttackComplete("ActorMovementSequence:CompleteOrders");
    }
}
