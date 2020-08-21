using BattleTech;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
// ReSharper disable All

// thank you Frosty IRBTModUtils CustomDialog
// https://github.com/IceRaptor/IRBTModUtils
namespace PanicSystem.Components.IRBTModUtilsCustomDialog {
    public static class ModState {

        public static CombatGameState Combat;
        public static Queue DialogueQueue = new Queue();
        public static bool IsDialogStackActive = false;
        public static Dictionary<string, Sprite> Portraits = new Dictionary<string, Sprite>();

        public static void Reset() {
            // Reinitialize state
            DialogueQueue.Clear();
            IsDialogStackActive = false;
        }
    }

}
