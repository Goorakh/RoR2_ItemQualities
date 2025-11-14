using HG;
using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using RoR2BepInExPack.GameAssetPathsBetter;
using System;
using UnityEngine;
using UnityEngine.Networking;

namespace ItemQualities.Items
{
    static class Plant
    {
        [SystemInitializer]
        static void Init()
        {
            AddressableUtil.LoadAssetAsync<GameObject>(RoR2_Base_Plant.InterstellarDeskPlant_prefab).OnSuccess(deskPlantPrefab =>
            {
                deskPlantPrefab.EnsureComponent<GenericOwnership>();
            });

            IL.RoR2.GlobalEventManager.OnCharacterDeath += GlobalEventManager_OnCharacterDeath;
            On.RoR2.DeskPlantController.MainState.OnEnter += MainState_OnEnter;
        }

        static void GlobalEventManager_OnCharacterDeath(ILContext il)
        {
            if (!il.Method.TryFindParameter<DamageReport>(out ParameterDefinition damageReportParameter))
            {
                Log.Error("Failed to find DamageReport parameter");
                return;
            }

            ILCursor c = new ILCursor(il);

            if (!c.TryFindNext(out ILCursor[] foundCursors,
                               x => x.MatchLdsfld(typeof(RoR2Content.Items), nameof(RoR2Content.Items.Plant)),
                               x => x.MatchCallOrCallvirt(typeof(NetworkServer), nameof(NetworkServer.Spawn))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Goto(foundCursors[1].Next, MoveType.Before); // call NetworkServer.Spawn

            c.Emit(OpCodes.Dup);
            c.Emit(OpCodes.Ldarg, damageReportParameter);
            c.EmitDelegate<Action<GameObject, DamageReport>>(onSpawnPlant);

            static void onSpawnPlant(GameObject plantObj, DamageReport damageReport)
            {
                if (plantObj && plantObj.TryGetComponent(out GenericOwnership genericOwnership))
                {
                    genericOwnership.ownerObject = damageReport?.attacker;
                }
            }
        }

        static void MainState_OnEnter(On.RoR2.DeskPlantController.MainState.orig_OnEnter orig, EntityStates.BaseState _self)
        {
            orig(_self);

            if (!NetworkServer.active)
                return;

            try
            {
                DeskPlantController.MainState self = (DeskPlantController.MainState)_self;
                if (self.deskplantWard && self.deskplantWard.TryGetComponent(out HealingWard healingWard))
                {
                    GameObject owner = null;
                    if (self.TryGetComponent(out GenericOwnership genericOwnership))
                    {
                        owner = genericOwnership.ownerObject;
                    }

                    CharacterBody ownerObject = owner ? owner.GetComponent<CharacterBody>() : null;
                    Inventory ownerInventory = ownerObject ? ownerObject.inventory : null;

                    ItemQualityCounts plant = ItemQualitiesContent.ItemQualityGroups.Plant.GetItemCounts(ownerInventory);
                    if (plant.TotalQualityCount > 0)
                    {
                        float healingRateIncrease = (0.15f * plant.UncommonCount) +
                                                    (0.30f * plant.RareCount) +
                                                    (0.60f * plant.EpicCount) +
                                                    (1.00f * plant.LegendaryCount);

                        if (healingRateIncrease > 0)
                        {
                            healingWard.interval /= 1f + healingRateIncrease;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error_NoCallerPrefix(e);
            }
        }
    }
}
