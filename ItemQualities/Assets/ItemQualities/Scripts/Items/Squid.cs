using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
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
        }

        static void GlobalEventManager_OnInteractionBegin(ILContext il)
        {
            if (!il.Method.TryFindParameter<Interactor>(out ParameterDefinition interactorParameter))
            {
                Log.Error("Failed to find Interactor parameter");
                return;
            }

            ILCursor c = new ILCursor(il);

            int squidDirectorSpawnRequestLocalIndex = -1;
            if (!c.TryFindNext(out ILCursor[] foundCursors,
                               x => x.MatchLdsfld(typeof(RoR2Content.Items), nameof(RoR2Content.Items.Squid)),
                               x => x.MatchNewobj<DirectorSpawnRequest>(),
                               x => x.MatchStloc(typeof(DirectorSpawnRequest), il, out squidDirectorSpawnRequestLocalIndex),
                               x => x.MatchStfld<DirectorSpawnRequest>(nameof(DirectorSpawnRequest.onSpawnedServer))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Goto(foundCursors[3].Next, MoveType.After);

            c.Emit(OpCodes.Ldloc, squidDirectorSpawnRequestLocalIndex);
            c.Emit(OpCodes.Ldarg, interactorParameter);
            c.EmitDelegate<Action<DirectorSpawnRequest, Interactor>>(handleQualitySquid);

            static void handleQualitySquid(DirectorSpawnRequest directorSpawnRequest, Interactor interactor)
            {
                if (directorSpawnRequest == null)
                    return;

                CharacterBody interactorBody = interactor ? interactor.GetComponent<CharacterBody>() : null;
                Inventory interactorInventory = interactorBody ? interactorBody.inventory : null;

                ItemQualityCounts squid = ItemQualitiesContent.ItemQualityGroups.Squid.GetItemCountsEffective(interactorInventory);
                if (squid.TotalQualityCount > 0)
                {
                    directorSpawnRequest.onSpawnedServer += (SpawnCard.SpawnResult result) =>
                    {
                        if (!result.success || !result.spawnedInstance)
                            return;

                        if (result.spawnedInstance.TryGetComponent(out CharacterMaster spawnedMaster) && spawnedMaster.inventory)
                        {
                            int boostHpCount = (2 * squid.UncommonCount) +
                                               (4 * squid.RareCount) +
                                               (8 * squid.EpicCount) +
                                               (15 * squid.LegendaryCount);

                            int boostDamageCount = (1 * squid.UncommonCount) +
                                                   (3 * squid.RareCount) +
                                                   (6 * squid.EpicCount) +
                                                   (10 * squid.LegendaryCount);

                            if (boostDamageCount > 0)
                            {
                                spawnedMaster.inventory.GiveItemPermanent(RoR2Content.Items.BoostDamage, boostDamageCount);
                            }

                            if (boostHpCount > 0)
                            {
                                spawnedMaster.inventory.GiveItemPermanent(RoR2Content.Items.BoostHp, boostHpCount);

                                float hpBoostPercent = boostHpCount * 0.1f;
                                int healthDecayDuration = spawnedMaster.inventory.GetItemCountPermanent(RoR2Content.Items.HealthDecay);
                                if (healthDecayDuration > 0)
                                {
                                    int newHealthDecayDuration = Mathf.RoundToInt(healthDecayDuration * (1f + hpBoostPercent));
                                    if (newHealthDecayDuration > healthDecayDuration)
                                    {
                                        spawnedMaster.inventory.GiveItemPermanent(RoR2Content.Items.HealthDecay, newHealthDecayDuration - healthDecayDuration);
                                    }
                                }
                            }
                        }
                    };
                }
            }
        }
    }
}
