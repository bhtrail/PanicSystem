using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BattleTech;
using BattleTech.Framework;
using BattleTech.UI;
using Harmony;
using HBS.Data;
using UnityEngine;
// ReSharper disable All

// thank you Frosty IRBTModUtils CustomDialog
// https://github.com/IceRaptor/IRBTModUtils
namespace PanicSystem.Components.IRBTModUtilsCustomDialog {
    // This classes liberally borrows CWolf's amazing MissionControl mod, in particular 
    //  https://github.com/CWolfs/MissionControl/blob/master/src/Core/DataManager.cs

    // A command control class that coordinates between the messages and the generated sequences
    public static class Coordinator {

        private static CombatGameState Combat;
        private static MessageCenter MessageCenter;
        private static CombatHUDDialogSideStack SideStack;
        internal static List<string> CallSigns;

        public static bool CombatIsActive {
            get { return Combat != null && SideStack != null; }
        }

        public static void OnCustomDialogMessage(MessageCenterMessage message) {
            PanicSystemDialogMessage msg = (PanicSystemDialogMessage)message;
            if (msg == null || msg.DialogueContent == null) { return; }

            ModState.DialogueQueue.Enqueue(msg);
            if (!ModState.IsDialogStackActive) {
                //LogDebug("No existing dialog sequence, publishing a new one.");
                ModState.IsDialogStackActive = true;
                MessageCenter.PublishMessage(
                    new AddParallelSequenceToStackMessage(new CustomDialogSequence(Combat, SideStack, false))
                    );
            } else {
                //LogDebug("Existing dialog sequence exists, skipping creation.");
            }
        }

        public static void OnCombatHUDInit(CombatGameState combat, CombatHUD combatHUD) {

            Combat = combat;
            MessageCenter = combat.MessageCenter;
            SideStack = combatHUD.DialogSideStack;

            if (CallSigns == null) {
                string filePath = Path.Combine(PanicSystem.modDirectory, PanicSystem.modSettings.Dialogue.CallsignsPath);
                //LogDebug($"Reading files from {filePath}");
                CallSigns = File.ReadAllLines(filePath).ToList();
            }
            //LogDebug($"Callsign count is: {CallSigns.Count}");

        }

        public static void OnCombatGameDestroyed() {

            Combat = null;
            MessageCenter = null;
            SideStack = null;
        }

        public static CastDef CreateCast(AbstractActor actor) {
            string castDefId = $"castDef_{actor.GUID}";
            if (actor.Combat.DataManager.CastDefs.Exists(castDefId)) {
                return actor.Combat.DataManager.CastDefs.Get(castDefId);
            }

            FactionValue actorFaction = actor?.team?.FactionValue;
            bool factionExists = actorFaction.Name != "INVALID_UNSET" && actorFaction.Name != "NoFaction" && 
                actorFaction.FactionDefID != null && actorFaction.FactionDefID.Length != 0 ? true : false;

            string employerFactionName = "Military Support";
            if (factionExists) {
                //LogDebug($"Found factionDef for id:{actorFaction}");
                string factionId = actorFaction?.FactionDefID;
                FactionDef employerFactionDef = UnityGameInstance.Instance.Game.DataManager.Factions.Get(factionId);
                if (employerFactionDef == null) { /*LogDebug($"Error finding FactionDef for faction with id '{factionId}'");*/ }
                else { employerFactionName = employerFactionDef.Name.ToUpper(); }
            } else {
                //LogDebug($"FactionDefID does not exist for faction: {actorFaction}");
            }

            CastDef newCastDef = new CastDef {
                // Temp test data
                FactionValue = actorFaction,
                firstName = $"{employerFactionName} -",
                showRank = false,
                showCallsign = true,
                showFirstName = true,
                showLastName = false
            };
            // DisplayName order is first, callsign, lastname

            newCastDef.id = castDefId;
            string portraitPath = GetRandomPortraitPath();
            newCastDef.defaultEmotePortrait.portraitAssetPath = portraitPath;
            if (actor.GetPilot() != null) {
                //LogDebug("Actor has a pilot, using pilot values.");
                Pilot pilot = actor.GetPilot();
                newCastDef.callsign = pilot.Callsign;

                // Hide the faction name if it's the player's mech
                if (actor.team.IsLocalPlayer) { newCastDef.showFirstName = false; }
            } else {
                //LogDebug("Actor is not piloted, generating castDef.");
                newCastDef.callsign = GetRandomCallsign();
            }
            //LogDebug($" Generated cast with callsign: {newCastDef.callsign} and DisplayName: {newCastDef.DisplayName()} using portrait: '{portraitPath}'");

            ((DictionaryStore<CastDef>)UnityGameInstance.BattleTechGame.DataManager.CastDefs).Add(newCastDef.id, newCastDef);

            return newCastDef;
        }

        public static DialogueContent BuildDialogueContent(CastDef castDef, string dialogue, Color dialogueColor)
        {

            if (castDef == null || String.IsNullOrEmpty(castDef.id) || castDef.defaultEmotePortrait == null || String.IsNullOrEmpty(castDef.defaultEmotePortrait.portraitAssetPath))
            {
                return null;
            }

            DialogueContent content = new DialogueContent(dialogue, dialogueColor, castDef.id, null, null,
                DialogCameraDistance.Medium, DialogCameraHeight.Default, 0);

            // ContractInitialize normally sets the castDef on the content... no need, since we have the actual ref
            Traverse castDefT = Traverse.Create(content).Field("castDef");
            castDefT.SetValue(castDef);

            // Initialize the active contract's team settings
            ApplyCastDef(content);

            // Load the default emote portrait
            Traverse dialogueSpriteCacheT = Traverse.Create(content).Field("dialogueSpriteCache");
            Dictionary<string, Sprite> dialogueSpriteCache = dialogueSpriteCacheT.GetValue<Dictionary<string, Sprite>>();
            dialogueSpriteCache[castDef.defaultEmotePortrait.portraitAssetPath] = ModState.Portraits[castDef.defaultEmotePortrait.portraitAssetPath];

            return content;
        }

        // Clone of DialogueContent::ApplyCastDef
        private static void ApplyCastDef(DialogueContent dialogueContent)
        {
            Contract contract = ModState.Combat.ActiveContract;

            if (dialogueContent.selectedCastDefId == CastDef.castDef_TeamLeader_Employer)
            {
                TeamOverride teamOverride = contract.GameContext.GetObject(GameContextObjectTagEnum.TeamEmployer) as TeamOverride;
                dialogueContent.selectedCastDefId = teamOverride.teamLeaderCastDefId;
            }
            else if (dialogueContent.selectedCastDefId == CastDef.castDef_TeamLeader_EmployersAlly)
            {
                TeamOverride teamOverride2 = contract.GameContext.GetObject(GameContextObjectTagEnum.TeamEmployersAlly) as TeamOverride;
                dialogueContent.selectedCastDefId = teamOverride2.teamLeaderCastDefId;
            }
            else if (dialogueContent.selectedCastDefId == CastDef.castDef_TeamLeader_Target)
            {
                TeamOverride teamOverride3 = contract.GameContext.GetObject(GameContextObjectTagEnum.TeamTarget) as TeamOverride;
                dialogueContent.selectedCastDefId = teamOverride3.teamLeaderCastDefId;
            }
            else if (dialogueContent.selectedCastDefId == CastDef.castDef_TeamLeader_TargetsAlly)
            {
                TeamOverride teamOverride4 = contract.GameContext.GetObject(GameContextObjectTagEnum.TeamTargetsAlly) as TeamOverride;
                dialogueContent.selectedCastDefId = teamOverride4.teamLeaderCastDefId;
            }
            else if (dialogueContent.selectedCastDefId == CastDef.castDef_TeamLeader_Neutral)
            {
                TeamOverride teamOverride5 = contract.GameContext.GetObject(GameContextObjectTagEnum.TeamNeutralToAll) as TeamOverride;
                dialogueContent.selectedCastDefId = teamOverride5.teamLeaderCastDefId;
            }
            else if (dialogueContent.selectedCastDefId == CastDef.castDef_TeamLeader_Hostile)
            {
                TeamOverride teamOverride6 = contract.GameContext.GetObject(GameContextObjectTagEnum.TeamHostileToAll) as TeamOverride;
                dialogueContent.selectedCastDefId = teamOverride6.teamLeaderCastDefId;
            }
        }
        private static string GetRandomCallsign() {
            return CallSigns[UnityEngine.Random.Range(0, CallSigns.Count)];
        }
        private static string GetRandomPortraitPath() {
            return PanicSystem.modSettings.Dialogue.Portraits[UnityEngine.Random.Range(0, PanicSystem.modSettings.Dialogue.Portraits.Length)];
        }

    }
}
