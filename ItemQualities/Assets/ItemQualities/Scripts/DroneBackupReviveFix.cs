using HG;
using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using System;
using UnityEngine;

namespace ItemQualities
{
    internal static class DroneBackupReviveFix
    {
        [InitDuringStartupPhase(GameInitPhase.DuringIntro)]
        private static void Init()
        {
            IL.RoR2.EquipmentSlot.FireDroneBackup += EquipmentSlot_FireDroneBackup;
        }

        private static void EquipmentSlot_FireDroneBackup(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            /*
             *  // characterMaster.gameObject.AddComponent<MasterSuicideOnTimer>();
             *  IL_00D2: ldloc.s   V_12
             *  IL_00D4: call      instance class [UnityEngine.CoreModule]UnityEngine.GameObject [UnityEngine.CoreModule]UnityEngine.Component::get_gameObject()
             *  IL_00D9: callvirt  instance !!0 [UnityEngine.CoreModule]UnityEngine.GameObject::AddComponent<class RoR2.MasterSuicideOnTimer>()
             */

            VariableDefinition droneMasterVar = null;
            if (!c.TryGotoNext(MoveType.AfterLabel,
                               x => x.MatchLdloc<CharacterMaster>(il, out droneMasterVar),
                               x => x.MatchCallOrCallvirt<Component>("get_" + nameof(Component.gameObject)),
                               x => x.MatchCallOrCallvirt(CommonReflectionCache.AddComponent.OfType<MasterSuicideOnTimer>.Method)))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Emit(OpCodes.Ldloc, droneMasterVar);
            c.EmitDelegate<Action<CharacterMaster>>(setupDroneReviveHandling);

            static void setupDroneReviveHandling(CharacterMaster droneMaster)
            {
                droneMaster.EnsureComponent<ResetMasterSuicideOnTimerOnRevive>();
            }
        }
    }
}
