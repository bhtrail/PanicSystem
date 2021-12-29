using System;
using System.Linq;
using BattleTech;
using PanicSystem.Patches;
using static PanicSystem.PanicSystem;
using static PanicSystem.Logger;
using static PanicSystem.Components.Controller;
using static PanicSystem.Helpers;
using Random = UnityEngine.Random;
#if NO_CAC
#else
using CustomAmmoCategoriesPatches;
#endif

// ReSharper disable ClassNeverInstantiated.Global

namespace PanicSystem.Components
{
    public class SavingThrows
    {
        public static bool SavedVsPanic(AbstractActor actor, float savingThrow)
        {
            try
            {
                AbstractActor defender = null;
                if (actor is Vehicle vehicle)
                {
                    if (!modSettings.VehiclesCanPanic)
                    {
                        return true;
                    }

                    defender = vehicle;
                }
                else if (actor is Mech mech)
                {
                    defender = mech;
                }

                if (defender == null)
                {
                    modLog.LogReport($"defender null, passing save. actor {actor} is type {actor.GetType()}");
                    return true;
                }

                if (modSettings.QuirksEnabled)
                {
                    if (defender is Mech m)
                    {
                        if (m.pilot.pilotDef.PilotTags.Contains("pilot_brave"))
                        {
                            savingThrow -= modSettings.BraveModifier;
                            modLog.LogReport($"{"Bravery",-20} | {modSettings.BraveModifier,10} | {savingThrow,10:F3}");
                        }
                    }
                }

                var index = GetActorIndex(defender);
                float panicModifier = GetPanicModifier(TrackedActors[index].PanicStatus);
                savingThrow *= panicModifier;
                savingThrow *= actor.GetPanicMultiplier();
                modLog.LogReport($"{"Panic multiplier",-20} | {panicModifier,10} | {savingThrow,10:F3}");
                savingThrow = (float) Math.Max(0f, Math.Round(savingThrow));

                if (savingThrow < 1)
                {
                    modLog.LogReport(new string('-', 46));
                    modLog.LogReport("Negative saving throw| skipping");
                    return true;
                }

                var roll = Random.Range(1, 100);
                modLog.LogReport(new string('-', 46));
                modLog.LogReport($"{"Saving throw",-20} | {savingThrow,-5}{roll,5} | {"Roll",10}");
                modLog.LogReport(new string('-', 46));
                SaySpamFloatie(defender, $"{$"{modSettings.PanicSpamSaveString}:{savingThrow}",-6} {$"{modSettings.PanicSpamRollString}:{roll}!",3}");

                // lower panic level on crit success
                if (roll == 100)
                {
                    modLog.LogReport("Critical success");
                    SaySpamFloatie(defender, $"{modSettings.PanicSpamCritSaveString}");
                    TrackedActors[index].PanicStatus--;
                    // just in case the status went down then back up on a crit save in the same round
                    TrackedActors[index].PanicWorsenedRecently = false;
                    return true;
                }

                if (!modSettings.AlwaysPanic &&
                    roll >= savingThrow)
                {
                    modLog.LogReport("Successful panic save");
                    SaySpamFloatie(defender, $"{modSettings.PanicSpamSaveString}!");
                    return true;
                }

                modLog.LogReport("Failed panic save");
                SaySpamFloatie(defender, $"{modSettings.PanicSpamFailString}!");

                var originalStatus = TrackedActors[index].PanicStatus;
                if (defender is Vehicle)
                {
                    TrackedActors[index].PanicStatus = PanicStatus.Panicked;
                }
                else
                {
                    TrackedActors[index].PanicStatus++;
                }

                TrackedActors[index].PanicWorsenedRecently = true;

                // check for panic crit
                if (roll == 1 ||
                    ActorHealth(defender) <= modSettings.MechHealthForCrit &&
                    roll < Convert.ToInt32(savingThrow) - modSettings.CritOver)
                {
                    modLog.LogReport("Critical failure on panic save");
                    defender.Combat.MessageCenter.PublishMessage(
                        new AddSequenceToStackMessage(
                            new ShowActorInfoSequence(defender, modSettings.PanicCritFailString, FloatieMessage.MessageNature.CriticalHit, true)));
                    // ejection can only occur from a stressed or panicked state where panicked requirement is achieved regardless
                    // no crit going from confident to panicked then ejection
                    TrackedActors[index].PanicStatus = PanicStatus.Panicked;
                }

                TrackedActors[index].PreventEjection = originalStatus < PanicStatus.Stressed;
            }
            catch (Exception ex)
            {
                modLog.LogReport(ex);
            }

            return false;
        }

        public static float GetSavingThrow(AbstractActor defender, AbstractActor attacker,int heatDamage,float damageIncludingHeatDamage)
        {
            var pilot = defender.GetPilot();
            var weapons = defender.Weapons;
            var gutsAndTacticsSum = defender.SkillGuts * modSettings.GutsEjectionResistPerPoint +
                                    defender.SkillTactics * modSettings.TacticsEjectionResistPerPoint;
            float totalMultiplier = 0;

            DrawHeader();
            modLog.LogReport($"{$"Unit health {ActorHealth(defender):F2}%",-20} | {"",10} |");

            if (defender is Mech defendingMech)
            {
                try
                {
                    //BleedLevelFactor - modsetting

                    if (pilot.StatCollection.GetValue<float>("BloodBank") > 0 &&
                        pilot.StatCollection.GetValue<float>("BloodCapacity") > 0)
                    {
                        var bloodFraction = pilot.StatCollection.GetValue<float>("BloodBank") / pilot.StatCollection.GetValue<float>("BloodCapacity");
                        if (bloodFraction < 1)
                        {
                            var bloodMulti = modSettings.BleedLevelFactor / bloodFraction;
                            totalMultiplier += bloodMulti;
                            modLog.LogReport($"{"Blood Level",-20} | {bloodMulti,10:F3} | {totalMultiplier,10:F3}");
                        }
                    }
                    if ((pilot.StatCollection.GetValue<float>("BleedingRate") * pilot.StatCollection.GetValue<float>("BleedingRateMulti")) > 0)
                    {
                        var bleedRate = pilot.StatCollection.GetValue<float>("BleedingRate") * pilot.StatCollection.GetValue<float>("BleedingRateMulti");
                        var bleedRateMulti = modSettings.BleedRateFactor * bleedRate;
                        totalMultiplier += bleedRateMulti;
                        modLog.LogReport($"{"Bleeding Rate",-20} | {bleedRateMulti,10:F3} | {totalMultiplier,10:F3}");
                    }
                    
                    if (modSettings.QuirksEnabled && attacker!=null &&
                            attacker is Mech mech &&
                            mech.MechDef.Chassis.ChassisTags.Contains("mech_quirk_distracting"))
                    {
                        totalMultiplier += modSettings.DistractingModifier;
                        modLog.LogReport($"{"Distracting mech",-20} | {modSettings.DistractingModifier,10:F3} | {totalMultiplier,10:F3}");
                    }
#if NO_CAC
                    if (modSettings.HeatDamageFactor > 0){
#else
                    if (modSettings.HeatDamageFactor > 0 && defender.isHasHeat()) { 
#endif
                        totalMultiplier += modSettings.HeatDamageFactor * heatDamage;
                        modLog.LogReport($"{$"Heat damage {heatDamage}",-20} | {modSettings.HeatDamageFactor * heatDamage,10:F3} | {totalMultiplier,10:F3}");
                    }

                    float percentPilot = PercentPilot(pilot);
                    if (percentPilot < 1)
                    {
                        totalMultiplier += modSettings.PilotHealthMaxModifier * percentPilot;
                        modLog.LogReport($"{"Pilot injuries",-20} | {modSettings.PilotHealthMaxModifier * percentPilot,10:F3} | {totalMultiplier,10:F3}");
                    }

                    if (defendingMech.IsUnsteady)
                    {
                        totalMultiplier += modSettings.UnsteadyModifier;
                        modLog.LogReport($"{"Unsteady",-20} | {modSettings.UnsteadyModifier,10} | {totalMultiplier,10:F3}");
                    }

                    if (defendingMech.IsFlaggedForKnockdown)
                    {
                        totalMultiplier += modSettings.UnsteadyModifier;
                        modLog.LogReport($"{"Knockdown",-20} | {modSettings.UnsteadyModifier,10} | {totalMultiplier,10:F3}");
                    }

                    if (modSettings.OverheatedModifier > 0 && defendingMech.OverheatLevel < defendingMech.CurrentHeat)
                    {
                        totalMultiplier += modSettings.OverheatedModifier;
                        modLog.LogReport($"{"Heat",-20} | {modSettings.OverheatedModifier,10:F3} | {totalMultiplier,10:F3}");
                    }

                    if (modSettings.ShutdownModifier > 0 && defendingMech.IsShutDown)
                    {
                        totalMultiplier += modSettings.ShutdownModifier;
                        modLog.LogReport($"{"Shutdown",-20} | {modSettings.ShutdownModifier,10:F3} | {totalMultiplier,10:F3}");
                    }

                    float percentHead = PercentHead(defendingMech);
                    if (percentHead < 1)
                    {
                        totalMultiplier += modSettings.HeadMaxModifier * (1 - percentHead);
                        modLog.LogReport($"{"Head",-20} | {modSettings.HeadMaxModifier * (1 - percentHead),10:F3} | {totalMultiplier,10:F3}");
                    }

                    float percentCenterTorso = PercentCenterTorso(defendingMech);
                    if (percentCenterTorso < 1)
                    {
                        totalMultiplier += modSettings.CenterTorsoMaxModifier * (1 - percentCenterTorso);
                        modLog.LogReport($"{"CT",-20} | {modSettings.CenterTorsoMaxModifier * (1 - percentCenterTorso),10:F3} | {totalMultiplier,10:F3}");
                    }

                    float percentLeftTorso = PercentLeftTorso(defendingMech);
                    if (percentLeftTorso < 1)
                    {
                        totalMultiplier += modSettings.SideTorsoMaxModifier * (1 - percentLeftTorso);
                        modLog.LogReport($"{"LT",-20} | {modSettings.SideTorsoMaxModifier * (1 - percentLeftTorso),10:F3} | {totalMultiplier,10:F3}");
                    }

                    float percentRightTorso = PercentRightTorso(defendingMech);
                    if (percentRightTorso < 1)
                    {
                        totalMultiplier += modSettings.SideTorsoMaxModifier * (1 - percentRightTorso);
                        modLog.LogReport($"{"RT",-20} | {modSettings.SideTorsoMaxModifier * (1 - percentRightTorso),10:F3} | {totalMultiplier,10:F3}");
                    }

                    float percentLeftLeg = PercentLeftLeg(defendingMech);
                    if (percentLeftLeg < 1)
                    {
                        totalMultiplier += modSettings.LeggedMaxModifier * (1 - percentLeftLeg);
                        modLog.LogReport($"{"LL",-20} | {modSettings.LeggedMaxModifier * (1 - percentLeftLeg),10:F3} | {totalMultiplier,10:F3}");
                    }

                    float percentRightLeg = PercentRightLeg(defendingMech);
                    if (percentRightLeg < 1)
                    {
                        totalMultiplier += modSettings.LeggedMaxModifier * (1 - percentRightLeg);
                        modLog.LogReport($"{"RL",-20} | {modSettings.LeggedMaxModifier * (1 - percentRightLeg),10:F3} | {totalMultiplier,10:F3}");
                    }

                    // alone
                    if (defendingMech.Combat.GetAllAlliesOf(defendingMech).TrueForAll(m => m.IsDead || m == defendingMech))
                    {
                        if (Random.Range(1, 5) == 0) // 20% chance of appearing
                        {
                            SaySpamFloatie(defendingMech, $"{modSettings.PanicSpamAloneString}");
                        }

                        totalMultiplier += modSettings.AloneModifier;
                        modLog.LogReport($"{"Alone",-20} | {modSettings.AloneModifier,10} | {totalMultiplier,10:F3}");
                    }else if(defendingMech.Combat.GetAllAlliesOf(defendingMech).Count()>0)
                    {
                        int alliesdead = defendingMech.Combat.GetAllAlliesOf(defendingMech).Where(m => m.IsDead).Count();
                        int alliestotal = defendingMech.Combat.GetAllAlliesOf(defendingMech).Count();

                        totalMultiplier += modSettings.AloneModifier*alliesdead/alliestotal;
                        modLog.LogReport($"{$"Alone {alliesdead}/{alliestotal}",-20} | {modSettings.AloneModifier * alliesdead / alliestotal,10:F3} | {totalMultiplier,10:F3}");
                    }
                }
                catch (Exception ex)
                {
                    // BOMB
                    modLog.LogReport(ex);
                    return -1f;
                }
            }

            // weaponless
            if (weapons.TrueForAll(w => w.DamageLevel != ComponentDamageLevel.Functional || !w.HasAmmo)) // only fully unusable
            {
                if (Random.Range(1, 5) == 1) // 20% chance of appearing
                {
                    SaySpamFloatie(defender, $"{modSettings.PanicSpamNoWeaponsString}");
                }

                totalMultiplier += modSettings.WeaponlessModifier;
                modLog.LogReport($"{"Weaponless",-20} | {modSettings.WeaponlessModifier,10} | {totalMultiplier,10:F3}");
            }

            // directly override the multiplier for vehicles
            if (modSettings.VehiclesCanPanic &&
                defender is Vehicle defendingVehicle)
            {
                
                float percentTurret = PercentTurret(defendingVehicle);
                if (percentTurret < 1)
                {
                    totalMultiplier += modSettings.VehicleDamageFactor * (1 - percentTurret);
                    modLog.LogReport($"{"T",-20} | {modSettings.VehicleDamageFactor * (1 - percentTurret),10:F3} | {totalMultiplier,10:F3}");
                }
                float percentLeft = PercentLeft(defendingVehicle);
                if (percentLeft < 1)
                {
                    totalMultiplier += modSettings.VehicleDamageFactor * (1 - percentLeft);
                    modLog.LogReport($"{"L",-20} | {modSettings.VehicleDamageFactor * (1 - percentLeft),10:F3} | {totalMultiplier,10:F3}");
                }
                float percentRight = PercentRight(defendingVehicle);
                if (percentRight < 1)
                {
                    totalMultiplier += modSettings.VehicleDamageFactor * (1 - percentRight);
                    modLog.LogReport($"{"R",-20} | {modSettings.VehicleDamageFactor * (1 - percentRight),10:F3} | {totalMultiplier,10:F3}");
                }
                float percentFront = PercentFront(defendingVehicle);
                if (percentFront < 1)
                {
                    totalMultiplier += modSettings.VehicleDamageFactor * (1 - percentFront);
                    modLog.LogReport($"{"F",-20} | {modSettings.VehicleDamageFactor * (1 - percentFront),10:F3} | {totalMultiplier,10:F3}");
                }
                float percentRear = PercentRear(defendingVehicle);
                if (percentRear < 1)
                {
                    totalMultiplier += modSettings.VehicleDamageFactor * (1 - percentRear);
                    modLog.LogReport($"{"B",-20} | {modSettings.VehicleDamageFactor * (1 - percentRear),10:F3} | {totalMultiplier,10:F3}");
                }
                modLog.LogReport($"{"Vehicle state",-20} | {modSettings.VehicleDamageFactor,10} | {totalMultiplier,10:F3}");

                // alone
                if (defendingVehicle.Combat.GetAllAlliesOf(defendingVehicle).TrueForAll(m => m.IsDead || m == defendingVehicle))
                {
                    if (Random.Range(1, 5) == 0) // 20% chance of appearing
                    {
                        SaySpamFloatie(defendingVehicle, $"{modSettings.PanicSpamAloneString}");
                    }

                    totalMultiplier += modSettings.AloneModifier;
                    modLog.LogReport($"{"Alone",-20} | {modSettings.AloneModifier,10} | {totalMultiplier,10:F3}");
                }
                else if (defendingVehicle.Combat.GetAllAlliesOf(defendingVehicle).Count() > 0)
                {
                    int alliesdead = defendingVehicle.Combat.GetAllAlliesOf(defendingVehicle).Where(m => m.IsDead).Count();
                    int alliestotal = defendingVehicle.Combat.GetAllAlliesOf(defendingVehicle).Count();

                    totalMultiplier += modSettings.AloneModifier * alliesdead / alliestotal;
                    modLog.LogReport($"{$"Alone {alliesdead}/{alliestotal}",-20} | {modSettings.AloneModifier * alliesdead / alliestotal,10:F3} | {totalMultiplier,10:F3}");
                }
            }

            var resolveModifier = modSettings.ResolveMaxModifier *
                (defender.Combat.LocalPlayerTeam.Morale - modSettings.MedianResolve) / modSettings.MedianResolve;

            if (modSettings.VehiclesCanPanic &&
                defender is Vehicle)
            {
                resolveModifier *= modSettings.VehicleResolveFactor;
            }

            totalMultiplier -= resolveModifier;
            modLog.LogReport($"{$"Resolve {defender.Combat.LocalPlayerTeam.Morale}",-20} | {resolveModifier * -1,10:F3} | {totalMultiplier,10:F3}");

            if (modSettings.VehiclesCanPanic &&
                defender is Vehicle)
            {
                gutsAndTacticsSum *= modSettings.VehicleGutAndTacticsFactor;
            }

            totalMultiplier -= gutsAndTacticsSum;

            modLog.LogReport($"{"Guts and Tactics",-20} | {$"-{gutsAndTacticsSum}",10} | {totalMultiplier,10:F3}");
            return totalMultiplier;
        }

        // false is punchin' out
        public static bool SavedVsEject(AbstractActor actor, float savingThrow)
        {
            modLog.LogReport("Panic save failure requires eject save");

            try
            {
                if(actor.IsPilotable && actor.GetPilot()!=null && actor.GetPilot().StatCollection.GetValue<bool>("CanEject") == false)
                {
                    modLog.LogReport($"Pilot CanEject Stat false - {(modSettings.ObeyPilotCanEjectStat ? "":"NOT")} obeying");
                    LogActor(actor,true);
                    if(modSettings.ObeyPilotCanEjectStat)
                        return true;
                }
                if (actor.IsPilotable && actor.GetPilot() != null && actor.GetPilot().pilotDef.PilotTags.Contains("pilot_cannot_eject"))
                {
                    modLog.LogReport($"Pilot pilot_cannot_eject Tag set - {(modSettings.ObeyPilotCannotEjectTag ? "" : "NOT")} obeying");
                    LogActor(actor, true);
                    if (modSettings.ObeyPilotCannotEjectTag)
                        return true;
                }
            }
            catch(Exception ex)
            {
                modLog.LogReport(ex);
            }

            var pilotTracker = TrackedActors.First(tracker => tracker.Guid == actor.GUID);
            if (pilotTracker.PreventEjection)
            {
                modLog.LogReport("Ejection forbidden after crit unless already stressed or panicked");
                pilotTracker.PreventEjection = false;
                return true;
            }

            DrawHeader();

            if (actor is Mech mech && modSettings.QuirksEnabled)
            {
                if (mech.pilot.pilotDef.PilotTags.Contains("pilot_dependable"))
                {
                    savingThrow -= modSettings.DependableModifier;
                    modLog.LogReport($"{"Dependable",-20} | {modSettings.DependableModifier,10} | {savingThrow,10:F3}");
                }
            }

            // calculate result
            if (modSettings.VehiclesCanPanic &&
                actor is Vehicle)
            {
                savingThrow = Math.Max(0f, savingThrow - modSettings.BaseVehicleEjectionResist);
                modLog.LogReport($"{"Base ejection resist",-20} | {modSettings.BaseVehicleEjectionResist,10} | {savingThrow,10:F3}");
            }
            else if (actor is Mech)
            {
                savingThrow = Math.Max(0f, savingThrow - modSettings.BaseEjectionResist);
                modLog.LogReport($"{"Base ejection resist",-20} | {modSettings.BaseEjectionResist,10} | {savingThrow,10:F3}");
            }

            savingThrow *= actor.GetPanicMultiplier();
            savingThrow = (float) Math.Round(savingThrow);

            modLog.LogReport($"{"Eject multiplier",-20} | {modSettings.EjectChanceFactor,10} | {savingThrow,10:F3}");
            var roll = Random.Range(1, 100);
            modLog.LogReport(new string('-', 46));
            modLog.LogReport($"{"Saving throw",-20} | {savingThrow,-5:###}{roll,5} | {"Roll",10}");
            modLog.LogReport(new string('-', 46));
            if (!modSettings.AlwaysPanic &&
                savingThrow < 1)
            {
                modLog.LogReport("Negative saving throw| skipping");
                SaySpamFloatie(actor, $"{modSettings.PanicSpamEjectResistString}");
                return true;
            }

            // cap the saving throw by the setting
            savingThrow = (int) Math.Min(savingThrow, modSettings.MaxEjectChance);

            SaySpamFloatie(actor, $"{modSettings.PanicSpamSaveString}:{savingThrow}  {modSettings.PanicSpamRollString}:{roll}!");
            if (!modSettings.AlwaysPanic &&
                roll >= savingThrow)
            {
                modLog.LogReport("Successful ejection save");
                SaySpamFloatie(actor, $"{modSettings.PanicSpamSaveString}!  {ActorHealth(actor):#.#}%");
                return true;
            }

            // TODO can it be written if (mech != null) ? I don't know and testing it is a PITA!
            if (actor is Mech m)
            {
                if (modSettings.QuirksEnabled && m.MechDef.Chassis.ChassisTags.Contains("mech_quirk_noeject"))
                {
                    modLog.LogReport("This mech can't eject (quirk)");
                    actor.Combat.MessageCenter.PublishMessage(
                        new AddSequenceToStackMessage(
                            new ShowActorInfoSequence(actor, "Mech quirk: Can't eject", FloatieMessage.MessageNature.PilotInjury, true)));
                    return true;
                }

                if (modSettings.QuirksEnabled && m.pilot.pilotDef.PilotTags.Contains("pilot_drunk") &&
                    m.pilot.pilotDef.TimeoutRemaining > 0)
                {
                    modLog.LogReport("Drunkard - not ejecting");
                    actor.Combat.MessageCenter.PublishMessage(
                        new AddSequenceToStackMessage(
                            new ShowActorInfoSequence(actor, "Pilot quirk: Drunkard won't eject", FloatieMessage.MessageNature.PilotInjury, true)));
                    return true;
                }
            }

            modLog.LogReport("Failed ejection save: Punchin\' Out!!");
            return false;
        }
    }
}
