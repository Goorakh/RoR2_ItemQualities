using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using R2API;
using RoR2;
using RoR2.Items;
using System;
using System.Collections;
using UnityEngine;

namespace ItemQualities.Items
{
    internal static class BeetleGland
    {
        [SystemInitializer]
        private static void Init()
        {
            IL.RoR2.Items.BeetleGlandBodyBehavior.FixedUpdate += BeetleGlandBodyBehavior_FixedUpdate;
        }

        private static void BeetleGlandBodyBehavior_FixedUpdate(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            VariableDefinition spawnRequestVar = null;
            if (!c.TryGotoNext(MoveType.After,
                               x => x.MatchNewobj<DirectorSpawnRequest>(),
                               x => x.MatchStloc<DirectorSpawnRequest>(il, out spawnRequestVar)))
            {
                Log.PatchError(il, "Failed to find patch location");
                return;
            }

            c.TryGotoNext(MoveType.After,
                          x => x.MatchStfld<DirectorSpawnRequest>(nameof(DirectorSpawnRequest.onSpawnedServer)));

            c.Emit(OpCodes.Ldarg_0);
            c.Emit(OpCodes.Ldloc, spawnRequestVar);
            c.EmitDelegate<Action<BeetleGlandBodyBehavior, DirectorSpawnRequest>>(setupSpawnRequest);

            static void setupSpawnRequest(BeetleGlandBodyBehavior beetleGlandBodyBehavior, DirectorSpawnRequest spawnRequest)
            {
                if (beetleGlandBodyBehavior && beetleGlandBodyBehavior.body && beetleGlandBodyBehavior.body.inventory)
                {
                    ItemQualityCounts beetleGland = beetleGlandBodyBehavior.body.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.BeetleGland);
                    beetleGland.BaseItemCount = 0;

                    if (beetleGland.TotalQualityCount > 0)
                    {
                        spawnRequest.onSpawnedServer += onSpawnedServer;

                        void onSpawnedServer(SpawnCard.SpawnResult spawnResult)
                        {
                            if (spawnResult.success &&
                                spawnResult.spawnedInstance &&
                                spawnResult.spawnedInstance.TryGetComponent(out CharacterMaster spawnedMaster))
                            {
                                spawnedMaster.inventory.GiveItemsPermanent(ItemQualitiesContent.ItemQualityGroups.BeetleGlandGuardItem, beetleGland);
                                spawnedMaster.inventory.GiveItemPermanent(ItemQualitiesContent.ItemQualityGroups.QualityTier.GetItemIndex(beetleGland.HighestQuality));
                            }
                        }
                    }
                }
            }
        }
    }

    public sealed class BeetleGlandGuardQualityItemBehavior : QualityItemBodyBehavior, IOnIncomingDamageServerReceiver
    {
        [ItemGroupAssociation(QualityItemBehaviorUsageFlags.Server)]
        private static ItemQualityGroup GetItemGroup() => ItemQualitiesContent.ItemQualityGroups.BeetleGlandGuardItem;

        private void OnEnable()
        {
            if (!ReferenceEquals(Body?.healthComponent, null))
            {
                Body.healthComponent.AddOnIncomingDamageServerReceiver(this);
            }
        }

        private void OnDisable()
        {
            if (!ReferenceEquals(Body?.healthComponent, null))
            {
                Body.healthComponent.AddOnIncomingDamageServerReceiver(this);
            }
        }

        public void OnIncomingDamageServer(DamageInfo damageInfo)
        {
            CharacterMaster ownerMaster = Body.master ? Body.master.minionOwnership.ownerMaster : null;
            if (!ownerMaster)
            {
                return;
            }

            CharacterBody ownerBody = ownerMaster.GetBody();
            if (!ownerBody)
            {
                return;
            }

            ref readonly ItemQualityCounts beetleGland = ref Stacks;

            int repeatCount = (beetleGland.UncommonCount * 1) +
                              (beetleGland.RareCount * 3) +
                              (beetleGland.EpicCount * 6) +
                              (beetleGland.LegendaryCount * 10);

            if (repeatCount > 0)
            {
                DamageInfo[] repeatDamageInfos = new DamageInfo[repeatCount];

                for (int i = 0; i < repeatCount; i++)
                {
                    DamageInfo repeatDamageInfo = damageInfo.ShallowCopy();

                    repeatDamageInfo.damageType = new DamageTypeCombo(DamageType.BypassArmor | DamageType.BypassBlock | DamageType.BypassOneShotProtection | DamageType.Silent, DamageTypeExtended.BypassDamageCalculations, DamageSource.NoneSpecified);
                    repeatDamageInfo.damageType.AddModdedDamageType(DamageTypes.ProcOnly);

                    repeatDamageInfo.delayedDamageSecondHalf = true;
                    repeatDamageInfo.firstHitOfDelayedDamageSecondHalf = false;

                    repeatDamageInfo.position = transform.InverseTransformPoint(repeatDamageInfo.position);

                    repeatDamageInfos[i] = repeatDamageInfo;
                }

                ownerBody.StartCoroutine(inflictRepeatProcs(ownerBody.healthComponent, repeatDamageInfos));
            }
        }

        private static IEnumerator inflictRepeatProcs(HealthComponent victim, DamageInfo[] repeatDamageInfos)
        {
            const float TotalDelay = 0.25f;

            WaitForSeconds cachedWaitForSeconds = new WaitForSeconds(TotalDelay / repeatDamageInfos.Length);

            foreach (DamageInfo repeatDamageInfo in repeatDamageInfos)
            {
                if (!victim)
                    break;

                repeatDamageInfo.position = victim.transform.TransformPoint(repeatDamageInfo.position);
                victim.TakeDamage(repeatDamageInfo);

                yield return cachedWaitForSeconds;
            }
        }
    }
}
