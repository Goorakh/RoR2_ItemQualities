using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using RoR2BepInExPack.GameAssetPathsBetter;
using System;
using UnityEngine;

namespace ItemQualities.Items
{
    static class Squid
    {
        [SystemInitializer]
        static void Init()
        {
            IL.RoR2.GlobalEventManager.OnInteractionBegin += GlobalEventManager_OnInteractionBegin;
            GlobalEventManager.onCharacterDeathGlobal += GlobalEventManager_onCharacterDeathGlobal;

            AddressableUtil.LoadAssetAsync<Material>(RoR2_Base_Squid.matSquidTurret_mat).OnSuccess(squidMaterial =>
            {
                squidMaterial.SetFloat(ShaderProperties._EmPower, 0);
            });
        }

        private static void GlobalEventManager_onCharacterDeathGlobal(DamageReport damageReport)
        {
            CharacterMaster attackerMaster = damageReport?.attackerMaster;
            if (!attackerMaster)
                return;
            Inventory inventory = attackerMaster.inventory;
            if (!inventory)
                return;

            int DroneUpgradeOnKillCount = inventory.GetItemCountEffective(ItemQualitiesContent.Items.DroneUpgradeOnKill);
            if (DroneUpgradeOnKillCount > 0 &&
            DroneUpgradeOnKillCount > inventory.GetItemCountEffective(DLC3Content.Items.DroneUpgradeHidden) &&
            RollUtil.CheckRoll(DroneUpgradeOnKillCount * 10 + 10, attackerMaster, damageReport.damageInfo.procChainMask.HasProc(ProcType.SureProc)))
            {
                inventory.GiveItemPermanent(DLC3Content.Items.DroneUpgradeHidden);
            }
        }

        static void GlobalEventManager_OnInteractionBegin(ILContext il)
        {
            if (!il.Method.TryFindParameter<Interactor>(out ParameterDefinition interactorParameter))
            {
                Log.Error("Failed to find Interactor parameter");
                return;
            }

            ILCursor c = new ILCursor(il);

            VariableDefinition squidDirectorSpawnRequestVar = null;
            if (!c.TryFindNext(out ILCursor[] foundCursors,
                               x => x.MatchLdsfld(typeof(RoR2Content.Items), nameof(RoR2Content.Items.Squid)),
                               x => x.MatchNewobj<DirectorSpawnRequest>(),
                               x => x.MatchStloc(typeof(DirectorSpawnRequest), il, out squidDirectorSpawnRequestVar),
                               x => x.MatchStfld<DirectorSpawnRequest>(nameof(DirectorSpawnRequest.onSpawnedServer))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Goto(foundCursors[3].Next, MoveType.After);

            c.Emit(OpCodes.Ldloc, squidDirectorSpawnRequestVar);
            c.Emit(OpCodes.Ldarg, interactorParameter);
            c.EmitDelegate<Action<DirectorSpawnRequest, Interactor>>(handleQualitySquid);

            static void handleQualitySquid(DirectorSpawnRequest directorSpawnRequest, Interactor interactor)
            {
                if (directorSpawnRequest == null)
                    return;

                CharacterBody interactorBody = interactor ? interactor.GetComponent<CharacterBody>() : null;
                Inventory interactorInventory = interactorBody ? interactorBody.inventory : null;
                if (!interactorInventory)
                    return;

                ItemQualityCounts squid = interactorInventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.Squid);
                if (squid.TotalQualityCount > 0)
                {
                    directorSpawnRequest.onSpawnedServer += (SpawnCard.SpawnResult result) =>
                    {
                        if (!result.success || !result.spawnedInstance)
                            return;

                        if (result.spawnedInstance.TryGetComponent(out CharacterMaster spawnedMaster) && spawnedMaster.inventory)
                        {
                            spawnedMaster.inventory.GiveItemPermanent(ItemQualitiesContent.Items.DroneUpgradeOnKill, (int) squid.HighestQuality + 1);

                            int boostDamageCount = (3 * squid.UncommonCount) +
                                                   (4 * squid.RareCount) +
                                                   (5 * squid.EpicCount) +
                                                   (6 * squid.LegendaryCount);

                            if (boostDamageCount > 0)
                            {
                                spawnedMaster.inventory.GiveItemPermanent(RoR2Content.Items.BoostDamage, boostDamageCount);
                            }
                        }
                    };
                }
            }
        }
    }
}
