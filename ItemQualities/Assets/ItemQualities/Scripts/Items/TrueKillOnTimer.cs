using ItemQualities.Utilities.Extensions;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using RoR2.Items;
using System;

namespace ItemQualities.Items
{
    internal static class TrueKillOnTimer
    {
        [SystemInitializer]
        private static void Init()
        {
            MasterSummon.onServerMasterSummonGlobal += onServerMasterSummonGlobal;

            IL.RoR2.DroneCombinerController.TryGetCombinableDrones += DroneCombinerController_TryGetCombinableDrones;
            IL.RoR2.DroneScrapperController.AssignDronesFromInteractor += DroneScrapperController_AssignDronesFromInteractor;
        }

        private static bool hasKillTimer(CharacterBody body)
        {
            return body && body.inventory && body.inventory.GetItemCountEffective(ItemQualitiesContent.Items.TrueKillOnTimer) > 0;
        }

        private static void onServerMasterSummonGlobal(MasterSummon.MasterSummonReport summonReport)
        {
            if (!summonReport.masterSummon?.summonerBodyObject || !summonReport.summonMasterInstance)
                return;

            if (!summonReport.masterSummon.summonerBodyObject.TryGetComponent(out CharacterBody summonerBody) || !summonerBody.inventory)
                return;

            int summonerTrueKillTimer = summonerBody.inventory.GetItemCountEffective(ItemQualitiesContent.Items.TrueKillOnTimer);
            if (summonerTrueKillTimer > 0)
            {
                summonReport.summonMasterInstance.inventory.ResetItemPermanent(ItemQualitiesContent.Items.TrueKillOnTimer);
                summonReport.summonMasterInstance.inventory.GiveItemPermanent(ItemQualitiesContent.Items.TrueKillOnTimer, summonerTrueKillTimer);
            }
        }

        private static void DroneCombinerController_TryGetCombinableDrones(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            /*
             *  IL_007A: ldloc.s   V_8
             *  IL_007C: ldfld     valuetype RoR2.CharacterBody/BodyFlags RoR2.CharacterBody::bodyFlags
             *  IL_0081: ldc.i4    4194304
             *  IL_0086: and
             *  IL_0087: brfalse.s IL_00F1
             */

            VariableDefinition droneBodyVar = null;
            ILLabel invalidDroneLabel = null;
            if (!c.TryGotoNext(MoveType.After,
                               x => x.MatchLdloc<CharacterBody>(il, out droneBodyVar),
                               x => x.MatchLdfld<CharacterBody>(nameof(CharacterBody.bodyFlags)),
                               x => x.MatchLdcI4((int)CharacterBody.BodyFlags.Drone),
                               x => x.MatchAnd(),
                               x => x.MatchBrfalse(out invalidDroneLabel)))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Emit(OpCodes.Ldloc, droneBodyVar);
            c.EmitDelegate<Func<CharacterBody, bool>>(hasKillTimer);
            c.Emit(OpCodes.Brtrue, invalidDroneLabel);
        }

        private static void DroneScrapperController_AssignDronesFromInteractor(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            /*
             *  IL_006F: ldloc.3
             *  IL_0070: callvirt  instance class RoR2.Inventory RoR2.CharacterBody::get_inventory()
             *  IL_0075: ldsfld    class RoR2.ItemDef RoR2.DLC1Content/Items::GummyCloneIdentifier
             *  IL_007A: callvirt  instance int32 RoR2.Inventory::GetItemCountEffective(class RoR2.ItemDef)
             *  IL_007F: ldc.i4.0
             *  IL_0080: bgt.s     IL_00E9
             */

            VariableDefinition droneBodyVar = null;
            ILLabel invalidDroneLabel = null;
            if (!c.TryGotoNext(MoveType.After,
                               x => x.MatchLdloc<CharacterBody>(il, out droneBodyVar),
                               x => x.MatchCallOrCallvirt<CharacterBody>("get_" + nameof(CharacterBody.inventory)),
                               x => x.MatchLdsfld(typeof(DLC1Content.Items), nameof(DLC1Content.Items.GummyCloneIdentifier)),
                               x => x.MatchCallOrCallvirt<Inventory>(nameof(Inventory.GetItemCountEffective)),
                               x => x.MatchLdcI4(out _),
                               x => x.MatchBgt(out invalidDroneLabel)))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Emit(OpCodes.Ldloc, droneBodyVar);
            c.EmitDelegate<Func<CharacterBody, bool>>(hasKillTimer);
            c.Emit(OpCodes.Brtrue, invalidDroneLabel);
        }
    }

    public sealed class TrueKillOnTimerBehavior : BaseItemBodyBehavior
    {
        [ItemDefAssociation(useOnServer = true, useOnClient = false)]
        private static ItemDef GetItemDef()
        {
            return ItemQualitiesContent.Items.TrueKillOnTimer;
        }

        private void OnEnable()
        {
            if (body.master && body.master.TryGetComponent(out DroneRepairMaster droneRepairMaster))
            {
                // Prevent ghost Operator drones from repairing after dying
                // This value should not be restored since that would repair it after this component is removed
                droneRepairMaster.DoNotRepair = true;
                body.master.destroyOnBodyDeath = true;
            }
        }

        private void FixedUpdate()
        {
            // This assumes the item was given at spawn
            float timer = body.master ? body.master.currentLifeStopwatch : body.localStartTime.timeSince;
            if (timer >= stack)
            {
                if (body.master)
                {
                    body.master.TrueKill();
                }
                else
                {
                    body.healthComponent.Suicide();
                }
            }
        }
    }
}
