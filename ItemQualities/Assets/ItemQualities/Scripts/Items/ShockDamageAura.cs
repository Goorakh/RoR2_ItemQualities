using HG;
using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using RoR2.UI;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ItemQualities.Items
{
    internal static class ShockDamageAura
    {
        [SystemInitializer]
        private static void Init()
        {
            IL.RoR2.Inventory.UpdateEffectiveItemStacks += UpdateEffectiveItemStacks;
            On.RoR2.Inventory.CalculateEffectiveItemStacks += CalculateEffectiveItemStacks;
            On.RoR2.Inventory.HandleInventoryChanged += HandleInventoryChanged;
            On.RoR2.UI.ItemIcon.SetItemIndex_ItemIndex_int_float += SetItemIndex;
        }

        private static void SetItemIndex(On.RoR2.UI.ItemIcon.orig_SetItemIndex_ItemIndex_int_float orig, ItemIcon self, ItemIndex newItemIndex, int newItemCount, float newDurationPercent)
        {
            orig(self, newItemIndex, newItemCount, newDurationPercent);

            HUD hud = self.GetComponentInParent<HUD>();
            if (!hud || !hud.targetMaster)
                return;

            if (!hud.targetMaster.TryGetComponentCached(out CharacterMasterExtraStatsTracker extraStats))
                return;

            if (extraStats.ConductorItemStacks.GetStackValue(newItemIndex) > 0)
            {
                self.spriteAsNumberManager.SetSpriteColor(ColorCatalog.GetColor(ColorCatalog.ColorIndex.BossItem));
            }
            else
            {
                self.spriteAsNumberManager.SetSpriteColor(Color.white);
            }
        }

        private static void HandleInventoryChanged(On.RoR2.Inventory.orig_HandleInventoryChanged orig, Inventory self)
        {
            if (!self.TryGetComponentCached(out CharacterMasterExtraStatsTracker extraStats))
            {
                orig(self);
                return;
            }

            ItemQualityCounts shockDamageAura = self.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.ShockDamageAura);
            if (shockDamageAura.TotalQualityCount == 0)
            {
                using var _ = ListPool<ItemIndex>.RentCollection(out List<ItemIndex> conductorItemIndices);
                extraStats.ConductorItemStacks.GetNonZeroIndicesFixed(conductorItemIndices);
                extraStats.ConductorItemStacks.Clear();

                foreach (ItemIndex itemIndex in conductorItemIndices)
                {
                    self.UpdateEffectiveItemStacks(itemIndex);
                }

                orig(self);
                return;
            }

            int conductorIndex = -1;
            List<ItemIndex> itemAcquisitionOrder = self.itemAcquisitionOrder;

            for (int i = 0; i < itemAcquisitionOrder.Count; i++)
            {
                ItemQualityGroupIndex shockDamageAuraGroup = QualityCatalog.FindItemQualityGroupIndex(itemAcquisitionOrder[i]);
                if (shockDamageAuraGroup == ItemQualitiesContent.ItemQualityGroups.ShockDamageAura.GroupIndex)
                {
                    conductorIndex = i;
                    break;
                }
            }

            if (conductorIndex == -1)
            {
                orig(self);
                return;
            }

            int itemBuffCount = shockDamageAura.HighestQuality switch
            {
                QualityTier.Uncommon => 2,
                QualityTier.Rare => 3,
                QualityTier.Epic => 4,
                QualityTier.Legendary => 5,
                _ => 0,
            };

            int extraStacks = (shockDamageAura.UncommonCount * 2) +
                              (shockDamageAura.RareCount * 3) +
                              (shockDamageAura.EpicCount * 4) +
                              (shockDamageAura.LegendaryCount * 5);

            extraStats.ConductorItemStacks.Clear();
            for (int i = 0; i < itemBuffCount; i++)
            {
                if (i >= itemAcquisitionOrder.Count)
                    break;

                int duplicateIndex = (conductorIndex - i - 1);
                if (duplicateIndex < 0)
                {
                    duplicateIndex += itemAcquisitionOrder.Count;
                }

                ItemIndex itemIndex = itemAcquisitionOrder[duplicateIndex];
                ItemDef itemDef = ItemCatalog.GetItemDef(itemIndex);
                ItemQualityGroupIndex itemGroup = QualityCatalog.FindItemQualityGroupIndex(itemIndex);

                if (itemGroup == ItemQualitiesContent.ItemQualityGroups.ShockDamageAura.GroupIndex || // Don't duplicate itself

                    // These items only work as "real" items (permanent, temp), adding effective count won't do anything here.
                    // Add any consumable or key-like items here.
                    itemGroup == ItemQualitiesContent.ItemQualityGroups.TreasureCache.GroupIndex ||
                    itemGroup == ItemQualitiesContent.ItemQualityGroups.TreasureCacheVoid.GroupIndex ||
                    itemGroup == ItemQualitiesContent.ItemQualityGroups.HealingPotion.GroupIndex ||
                    itemGroup == ItemQualitiesContent.ItemQualityGroups.TeleportOnLowHealth.GroupIndex ||
                    itemGroup == ItemQualitiesContent.ItemQualityGroups.LowerPricedChests.GroupIndex ||
                    itemGroup == ItemQualitiesContent.ItemQualityGroups.ExtraLife.GroupIndex ||
                    itemGroup == ItemQualitiesContent.ItemQualityGroups.ExtraLifeVoid.GroupIndex ||

                    // Don't duplicate scrap
                    itemDef.ContainsTag(ItemTag.Scrap) ||

                    // Don't duplicate any objective-related items, these are likely used as a kind of "currency" or "key" and won't work when duplicated
                    itemDef.ContainsTag(ItemTag.ObjectiveRelated) ||

                    // If item cannot be temporary, odds are it only works as permanent, so our fake item likely won't work either
                    itemDef.DoesNotContainTag(ItemTag.CanBeTemporary) ||

                    // No consumed items
                    itemDef.tier == ItemTier.NoTier)
                {
                    itemBuffCount++;
                    continue;
                }

                extraStats.ConductorItemStacks.SetStackValue(itemIndex, extraStacks);
                self.UpdateEffectiveItemStacks(itemIndex);
            }

            orig(self);
        }

        private static int CalculateEffectiveItemStacks(On.RoR2.Inventory.orig_CalculateEffectiveItemStacks orig, Inventory self, ItemIndex itemIndex)
        {
            int result = orig(self, itemIndex);

            if (self.TryGetComponentCached(out CharacterMasterExtraStatsTracker extraStats))
            {
                result = HGMath.IntSafeAdd(result, extraStats.ConductorItemStacks.GetStackValue(itemIndex));
            }

            return result;
        }

        private static void UpdateEffectiveItemStacks(ILContext il)
        {
            if (!il.Method.TryFindParameter<ItemIndex>(out ParameterDefinition itemIndexParameter))
            {
                Log.Error("Failed to find ItemIndex parameter");
                return;
            }

            ILCursor c = new ILCursor(il);

            VariableDefinition stackNumVar = null;
            if (!c.TryGotoNext(MoveType.After,
                              x => x.MatchLdcI4(0),
                              x => x.MatchStloc(out _),
                              x => x.MatchLdcI4(0),
                              x => x.MatchStloc<int>(il, out stackNumVar)))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Emit(OpCodes.Ldarg_0);
            c.Emit(OpCodes.Ldloc, stackNumVar);
            c.Emit(OpCodes.Ldarg, itemIndexParameter);
            c.EmitDelegate<Func<Inventory, int, ItemIndex, int>>(addConductorStacks);
            c.Emit(OpCodes.Stloc, stackNumVar);

            static int addConductorStacks(Inventory self, int stackNum, ItemIndex itemIndex)
            {
                if (self.TryGetComponentCached(out CharacterMasterExtraStatsTracker extraStats))
                {
                    stackNum = HGMath.IntSafeAdd(stackNum, extraStats.ConductorItemStacks.GetStackValue(itemIndex));
                }

                return stackNum;
            }
        }
    }
}
