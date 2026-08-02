using HG;
using ItemQualities.Utilities;
using RoR2;
using RoR2.Items;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ItemQualities.Items
{
    public sealed class DroneWeaponsQualityItemBehavior : QualityItemBodyBehavior
    {
        [ItemGroupAssociation(QualityItemBehaviorUsageFlags.Server)]
        private static ItemQualityGroup GetItemGroup() => ItemQualitiesContent.ItemQualityGroups.DroneWeapons;

        private static readonly float droneReviveRollInterval = 1f;

        private QualityTier prevDroneCommanderQualityTier = QualityTier.None;

        private float droneReviveRollTimer;

        private void OnEnable()
        {
            MasterSummon.onServerMasterSummonGlobal += onServerMasterSummonGlobal;
        }

        private void OnDisable()
        {
            MasterSummon.onServerMasterSummonGlobal -= onServerMasterSummonGlobal;
            setDroneCommanderQualityTier(QualityTier.None);
        }

        private void FixedUpdate()
        {
            droneReviveRollTimer += Time.fixedDeltaTime;
            if (droneReviveRollTimer >= droneReviveRollInterval)
            {
                droneReviveRollTimer = 0f;

                ref readonly ItemQualityCounts droneWeapons = ref Stacks;

                float droneReviveChance;
                switch (droneWeapons.HighestQuality)
                {
                    case QualityTier.Uncommon:
                        droneReviveChance = 10f;
                        break;
                    case QualityTier.Rare:
                        droneReviveChance = 20f;
                        break;
                    case QualityTier.Epic:
                        droneReviveChance = 30f;
                        break;
                    case QualityTier.Legendary:
                        droneReviveChance = 40f;
                        break;
                    default:
                        droneReviveChance = 0f;
                        Log.Warning($"Quality tier {droneWeapons.HighestQuality} is not implemented");
                        break;
                }

                MinionOwnership.MinionGroup minionGroup = MinionOwnership.MinionGroup.FindGroup(Body.master.netId);
                if (minionGroup != null)
                {
                    for (int i = 0; i < minionGroup.memberCount; i++)
                    {
                        MinionOwnership minion = minionGroup.members[i];
                        if (minion && minion.TryGetComponent(out CharacterMaster minionMaster))
                        {
                            CharacterBody minionBody = minionMaster.GetBody();
                            if (minionBody && (minionBody.bodyFlags & CharacterBody.BodyFlags.Mechanical) != 0 && minionBody.healthComponent.alive)
                            {
                                if (Util.CheckRoll(droneReviveChance, Body.master))
                                {
                                    minionBody.AddTimedBuff(DLC2Content.Buffs.ExtraLifeBuff, droneReviveRollInterval);
                                }
                            }
                        }
                    }
                }
            }
        }

        private void onServerMasterSummonGlobal(MasterSummon.MasterSummonReport summonReport)
        {
            if (!ReferenceEquals(Body, summonReport.leaderBodyInstance))
                return;

            Inventory summonInventory = summonReport.summonMasterInstance ? summonReport.summonMasterInstance.inventory : null;

            if (summonReport.summonBodyInstance && (summonReport.summonBodyInstance.bodyFlags & CharacterBody.BodyFlags.Mechanical) != 0)
            {
                ref readonly ItemQualityCounts droneWeapons = ref Stacks;

                float eliteChance = (droneWeapons.UncommonCount * 10f) +
                                    (droneWeapons.RareCount * 25f) +
                                    (droneWeapons.EpicCount * 50f) +
                                    (droneWeapons.LegendaryCount * 75f);

                int eliteCount = RollUtil.GetOverflowRoll(eliteChance, Body.master, false);
                if (eliteCount > 0)
                {
                    if (summonInventory)
                    {
                        summonInventory.GiveItemPermanent(ItemQualitiesContent.Items.QualityDroneWeaponsRandomElite, eliteCount);
                    }
                }
            }
        }

        protected override void OnStacksChanged()
        {
            base.OnStacksChanged();

            setDroneCommanderQualityTier(Stacks.HighestQuality);
        }

        private void setDroneCommanderQualityTier(QualityTier qualityTier)
        {
            if (prevDroneCommanderQualityTier == qualityTier)
                return;

            ItemIndex fromItemIndex = ItemQualitiesContent.ItemQualityGroups.QualityTier.GetItemIndex(prevDroneCommanderQualityTier);
            ItemIndex toItemIndex = ItemQualitiesContent.ItemQualityGroups.QualityTier.GetItemIndex(qualityTier);

            if (Body.master.deployablesList != null)
            {
                foreach (DeployableInfo deployableInfo in Body.master.deployablesList)
                {
                    if (deployableInfo.slot == DeployableSlot.DroneWeaponsDrone &&
                        deployableInfo.deployable &&
                        deployableInfo.deployable.TryGetComponent(out CharacterBody droneCommanderBody) &&
                        droneCommanderBody.inventory)
                    {
                        if (fromItemIndex != ItemIndex.None)
                        {
                            new Inventory.ItemTransformation
                            {
                                originalItemIndex = fromItemIndex,
                                newItemIndex = toItemIndex,
                                minToTransform = 1,
                                maxToTransform = 1,
                                allowWhenDisabled = true,
                                transformationType = ItemTransformationTypeIndex.None,
                            }.TryTransform(droneCommanderBody.inventory, out _);
                        }
                        else
                        {
                            droneCommanderBody.inventory.GiveItemPermanent(toItemIndex);
                        }
                    }
                }
            }

            prevDroneCommanderQualityTier = qualityTier;
        }
    }

    public sealed class QualityDroneWeaponsRandomEliteBehavior : BaseItemBodyBehavior
    {
        [ItemDefAssociation(useOnServer = true, useOnClient = false)]
        private static ItemDef GetItemDef() => ItemQualitiesContent.Items.QualityDroneWeaponsRandomElite;

        private static BuffIndex[] allEliteBuffIndices = Array.Empty<BuffIndex>();

        [InitDuringStartupPhase(GameInitPhase.PostProgressBar)]
        private static void InitEliteBuffs()
        {
            HashSet<BuffIndex> validEliteBuffIndices = new HashSet<BuffIndex>(EliteCatalog.eliteList.Count);

            foreach (EliteIndex eliteIndex in EliteCatalog.eliteList)
            {
                EliteDef eliteDef = EliteCatalog.GetEliteDef(eliteIndex);
                if (!eliteDef || !eliteDef.eliteEquipmentDef || !eliteDef.eliteEquipmentDef.passiveBuffDef)
                    continue;

                if (eliteDef.name.EndsWith("honor", StringComparison.OrdinalIgnoreCase))
                    continue;

                string nameModifierToken = eliteDef.modifierToken;
                if (string.IsNullOrEmpty(nameModifierToken) || Language.IsTokenInvalid(nameModifierToken))
                    continue;

                Log.Debug($"Including elite {eliteDef}");

                validEliteBuffIndices.Add(eliteDef.eliteEquipmentDef.passiveBuffDef.buffIndex);
            }

            validEliteBuffIndices.Remove(BuffCatalog.FindBuffIndex("bdEliteVoid")); // spawns enemy void bugs
            validEliteBuffIndices.Remove(BuffCatalog.FindBuffIndex("bdEliteCollective")); // ally shield blocks your attacks
            validEliteBuffIndices.Remove(BuffIndex.None); // just in case

            allEliteBuffIndices = validEliteBuffIndices.ToArray();
            Array.Sort(allEliteBuffIndices);
        }

        private BuffIndex[] eliteBuffsOrder = Array.Empty<BuffIndex>();

        private int prevEliteBuffCount;

        private void OnEnable()
        {
            eliteBuffsOrder = ArrayUtils.Clone(allEliteBuffIndices);
            Util.ShuffleArray(eliteBuffsOrder);

            refreshEliteBuffCount();
        }

        private void OnDisable()
        {
            setEliteBuffCount(0);
        }

        public override void OnInventoryRefresh()
        {
            base.OnInventoryRefresh();
            refreshEliteBuffCount();
        }

        private void refreshEliteBuffCount()
        {
            setEliteBuffCount(Math.Min(stack, eliteBuffsOrder.Length));
        }

        private void setEliteBuffCount(int count)
        {
            if (count == prevEliteBuffCount)
                return;

            if (count > prevEliteBuffCount)
            {
                for (int i = prevEliteBuffCount; i < count; i++)
                {
                    body.AddBuff(eliteBuffsOrder[i]);
                }
            }
            else // implied if (count < prevEliteBuffCount)
            {
                for (int i = prevEliteBuffCount - 1; i >= count; i--)
                {
                    body.RemoveBuff(eliteBuffsOrder[i]);
                }
            }

            prevEliteBuffCount = count;
        }
    }
}
