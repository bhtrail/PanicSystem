using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BattleTech;
using BattleTech.Achievements;
using Harmony;
using HBS;
using PanicSystem.Components;
using PanicSystem.Components.IRBTModUtilsCustomDialog;
using UnityEngine;
using static PanicSystem.Logger;
using static PanicSystem.PanicSystem;
using static PanicSystem.Components.Controller;
using static PanicSystem.Helpers;
using PanicSystem.Patches;
using Random = UnityEngine.Random;
using System.Reflection;
using BattleTech.Save.Core;

namespace PanicSystem.Components
{
    public static class DamageHandler
    {
        private const string MechCombatStatsTypeName = "MechCombatStats";

        private static Type mechCombatStatsType;

        private static Type playerMechStatsType;

        private static MethodInfo tryGetValue;

        private static MethodInfo incrementKillCount;

        private static object mechCombatStats;

        private static FieldInfo playerMechStatsField = typeof(CombatProcessor).GetField("playerMechStats", AccessTools.all);

        public static CombatProcessor CombatProcessor;

        static DamageHandler()
        {
            try
            {
                mechCombatStatsType = typeof(CombatProcessor).GetNestedType(MechCombatStatsTypeName, AccessTools.all);

                if (mechCombatStatsType is null)
                {
                    LogError($"MechCombatStats Type is null");
                }

                playerMechStatsType = typeof(Dictionary<,>).MakeGenericType(typeof(string), mechCombatStatsType);

                tryGetValue = playerMechStatsType.GetMethod("TryGetValue", AccessTools.all);

                incrementKillCount = mechCombatStatsType.GetMethod("IncrementKillCount", AccessTools.all);

                if (tryGetValue is null)
                    LogError("tryGetValue is null");

                if (incrementKillCount is null)
                {
                    LogError("incrementKillCount is null");
                }
            }
            catch (Exception e)
            {
                LogError($"[{DateTime.Now}] {e}\n");
            }
        }

        public static void ProcessDamage(AbstractActor actor, float damage, float directStructureDamage, int heatdamage)
        {
            if (ShouldSkipProcessing(actor))
            {
                return;
            }

            AbstractActor attacker = TurnDamageTracker.attackActor();//just for logging

            //LogReport(new string('═', 46));
            LogReport($"Damage to {actor.DisplayName}/{actor.Nickname}/{actor.GUID}");
            if (attacker != null)
            {
                LogReport($"Damage by {attacker.DisplayName}/{attacker.Nickname}/{attacker.GUID}");
            }

            AbstractActor defender = null;
            switch (actor)
            {
                case Vehicle _:
                    defender = (Vehicle)actor;
                    break;
                case Mech _:
                    defender = (Mech)actor;
                    break;
            }

            // a building or turret?
            if (defender == null)
            {
                LogDebug("Not a mech or vehicle");
                return;
            }

            if (defender.IsDead || defender.IsFlaggedForDeath)
            {
                LogDebug("He's dead Jim.....");
                return;
            }
            LogReport($"Damage >>> D: {damage:F3} DS: {directStructureDamage:F3} H: {heatdamage}");
            TurnDamageTracker.batchDamageDuringActivation(actor, damage, directStructureDamage, heatdamage);
        }

        public static void ProcessBatchedTurnDamage(AbstractActor actor)
        {
            int heatdamage = 0;

            if (ShouldSkipProcessing(actor))
            {
                return;
            }

            AbstractActor attacker = TurnDamageTracker.attackActor();
            LogReport($"\n{new string('═', 46)}");
            LogReport($"Damage to {actor.DisplayName}/{actor.Nickname}/{actor.GUID}");
            LogReport($"Damage by {attacker.DisplayName}/{attacker.Nickname}/{attacker.GUID}");

            // get the attacker in case they have mech quirks
            AbstractActor defender = null;
            switch (actor)
            {
                case Vehicle _:
                    defender = (Vehicle)actor;
                    break;
                case Mech _:
                    defender = (Mech)actor;
                    break;
            }

            // a building or turret?
            if (defender == null)
            {
                LogDebug("Not a mech or vehicle");
                return;
            }

            if (defender.IsDead || defender.IsFlaggedForDeath)
            {
                LogDebug("He's dead Jim.....");
                return;
            }

            var index = GetActorIndex(defender);

            if (modSettings.OneChangePerTurn &&
                TrackedActors[index].PanicWorsenedRecently)
            {
                LogDebug($"OneChangePerTurn {defender.Nickname} - abort");
                return;
            }

            float damageIncludingHeatDamage = 0;

            if (!modSettings.AlwaysPanic &&
                !ShouldPanic(defender, attacker, out heatdamage, out damageIncludingHeatDamage))
            {
                return;
            }

            // automatically eject a klutzy pilot on knockdown with an additional roll failing on 13
            if (defender.IsFlaggedForKnockdown)
            {
                var defendingMech = (Mech)defender;
                if (defendingMech.pilot.pilotDef.PilotTags.Contains("pilot_klutz"))
                {
                    if (Random.Range(1, 100) == 13)
                    {
                        defender.Combat.MessageCenter.PublishMessage(new AddSequenceToStackMessage
                            (new ShowActorInfoSequence(defender, "WOOPS!", FloatieMessage.MessageNature.Debuff, false)));
                        LogReport("Very klutzy!");
                        return;
                    }
                }
            }

            // store saving throw
            // check it against panic
            // check it again ejection
            var savingThrow = SavingThrows.GetSavingThrow(defender, attacker, heatdamage, damageIncludingHeatDamage);
            // panic saving throw
            if (SavingThrows.SavedVsPanic(defender, savingThrow))
            {
                return;
            }

            if (!modSettings.OneChangePerTurn)
            {
                TurnDamageTracker.resetDamageTrackerFor(defender);
            }

            // stop if pilot isn't Panicked
            if (TrackedActors[index].PanicStatus != PanicStatus.Panicked)
            {
                return;
            }

            // eject saving throw
            if (!modSettings.AlwaysPanic &&
                SavingThrows.SavedVsEject(defender, savingThrow))
            {
                return;
            }

            // ejecting
            // random phrase
            if (modSettings.EnableEjectPhrases &&
                defender is Mech &&
                Random.Range(1, 100) <= modSettings.EjectPhraseChance)
            {
                var ejectMessage = ejectPhraseList[Random.Range(0, ejectPhraseList.Count)];
                // thank you IRBTModUtils
                //LogDebug($"defender {defender}");
                defender.Combat.MessageCenter.PublishMessage(new PanicSystemDialogMessage(defender, null, 6));
            }

            // remove effects, to prevent exceptions that occur for unknown reasons
            var combat = UnityGameInstance.BattleTechGame.Combat;
            var effectsTargeting = combat.EffectManager.GetAllEffectsTargeting(defender);
            foreach (var effect in effectsTargeting)
            {
                // some effects removal throw, so silently drop them
                try
                {
                    defender.CancelEffect(effect);
                }
                catch
                {
                    // ignored
                }
            }

            if (modSettings.VehiclesCanPanic &&
                defender is Vehicle v)
            {
                // make the regular Pilot Ejected floatie not appear, for this ejection
                Patches.VehicleRepresentation.supressDeathFloatieOnce();
                defender.EjectPilot(defender.GUID, -1, DeathMethod.PilotEjection, true);
                defender.Combat.MessageCenter.PublishMessage(new PanicSystemDialogMessage(defender, null, 5));
            }
            else
            {
                defender.EjectPilot(defender.GUID, -1, DeathMethod.PilotEjection, false);
            }

            LogReport("Ejected");
            //LogDebug($"Runtime {stopwatch.Elapsed}");

            if (!modSettings.CountAsKills)
            {
                return;
            }

            //handle weird cases due to damage from all sources
            if (attacker.GUID == defender.GUID)
            {
                //killed himself - possibly mines or made a building land on his own head ;)
                LogReport("Self Kill not counting");
                return;
            }

            if (attacker.team.GUID == defender.team.GUID)
            {
                //killed a friendly
                LogReport("Friendly Fire, Same Team Kill, not counting");
                return;
            }

            if (TurnDamageTracker.EjectionAlreadyCounted(defender))
            {
                return;
            }

            try
            {
                // this seems pretty convoluted
                var attackerPilot = combat.AllMechs.Where(mech => mech.pilot.Team.IsLocalPlayer)
                    .Where(x => x.PilotableActorDef == attacker.PilotableActorDef).Select(y => y.pilot).FirstOrDefault();

                var statCollection = attackerPilot?.StatCollection;
                if (statCollection == null)
                {
                    return;
                }

                if (defender is Mech)
                {
                    // add UI icons.. and pilot history?   ... MechsKilled already incremented??
                    // TODO count kills recorded on pilot history so it's not applied twice -added a check above should work unless other mods are directly modifying stats
                    statCollection.Set("MechsKilled", attackerPilot.MechsKilled + 1);
                    var stat = statCollection.GetStatistic("MechsEjected");
                    if (stat == null)
                    {
                        statCollection.AddStatistic("MechsEjected", 1);
                    }
                    else
                    {
                        var value = stat.Value<int>();
                        statCollection.Set("MechsEjected", value + 1);
                    }
                }
                else if (modSettings.VehiclesCanPanic &&
                        defender is Vehicle)
                {
                    statCollection.Set("OthersKilled", attackerPilot.OthersKilled + 1);
                    var stat = statCollection.GetStatistic("VehiclesEjected");
                    if (stat == null)
                    {
                        statCollection.AddStatistic("VehiclesEjected", 1);
                        //return;
                    }
                    else
                    {

                        var value = stat.Value<int>();
                        statCollection.Set("VehiclesEjected", value + 1);
                    }
                }

                try
                {
                    object PlayerMechStats = playerMechStatsField.GetValue(CombatProcessor);
                    bool result = (bool)tryGetValue.Invoke(PlayerMechStats, new[] { attackerPilot.GUID, mechCombatStats });

                    if (result)
                    {
                        incrementKillCount.Invoke(mechCombatStats, null);
                    }
                }
                catch (Exception e)
                {
                    LogError(e);
                    // add achievement kill (more complicated)
                    var combatProcessors = Traverse.Create(UnityGameInstance.BattleTechGame.Achievements).Field("combatProcessors").GetValue<AchievementProcessor[]>();
                    var combatProcessor = combatProcessors.FirstOrDefault(x => x.GetType() == AccessTools.TypeByName("BattleTech.Achievements.CombatProcessor"));

                    // field is of type Dictionary<string, CombatProcessor.MechCombatStats>
                    var playerMechStats = Traverse.Create(combatProcessor).Field("playerMechStats").GetValue<IDictionary>();
                    if (playerMechStats != null)
                    {
                        foreach (DictionaryEntry kvp in playerMechStats)
                        {
                            if ((string)kvp.Key == attackerPilot.GUID)
                            {
                                Traverse.Create(kvp.Value).Method("IncrementKillCount").GetValue();
                            }
                        }
                    }
                }


                var r = attackerPilot.StatCollection.GetStatistic("MechsEjected") == null
                        ? 0
                        : attackerPilot.StatCollection.GetStatistic("MechsEjected").Value<int>();
                LogDebug($"{attackerPilot.Callsign} SetMechEjectionCount {r}");

                r = attackerPilot.StatCollection.GetStatistic("VehiclesEjected") == null
                    ? 0
                    : attackerPilot.StatCollection.GetStatistic("VehiclesEjected").Value<int>();
                LogDebug($"{attackerPilot.Callsign} SetVehicleEjectionCount {r}");
            }
            catch (Exception ex)
            {
                LogDebug(ex);
            }
        }
    }
}
