using HG;
using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using RoR2.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ItemQualities.Items
{
    internal static class ShockDamageAura
    {
        private static ItemIndex[] _duplicateBlacklist = Array.Empty<ItemIndex>();

        [SystemInitializer(typeof(ItemCatalog), typeof(ItemTierCatalog), typeof(QualityCatalog))]
        private static void Init()
        {
            IL.RoR2.Inventory.UpdateEffectiveItemStacks += UpdateEffectiveItemStacks;
            On.RoR2.Inventory.CalculateEffectiveItemStacks += CalculateEffectiveItemStacks;
            On.RoR2.Inventory.HandleInventoryChanged += HandleInventoryChanged;
            On.RoR2.UI.ItemIcon.SetItemIndex_SetItemIndexArgs += SetItemIndex;

            // Build item duplication blacklist
            {
                using var _ = SetPool<ItemIndex>.RentCollection(out HashSet<ItemIndex> duplicateBlacklist);

                void blacklistItemGroup(ItemQualityGroup itemGroup)
                {
                    for (QualityTier qualityTier = QualityTier.None; qualityTier < QualityTier.Count; qualityTier++)
                    {
                        ItemIndex itemIndex = itemGroup.GetItemIndex(qualityTier);
                        if (itemIndex != ItemIndex.None)
                        {
                            duplicateBlacklist.Add(itemIndex);
                        }
                    }
                }

                void blacklistItemTag(ItemTag itemTag)
                {
                    duplicateBlacklist.UnionWith(ItemCatalog.GetItemsWithTag(itemTag));
                }

                void blacklistItemTier(ItemTier itemTier)
                {
                    for (ItemIndex itemIndex = 0; (int)itemIndex < ItemCatalog.itemCount; itemIndex++)
                    {
                        ItemDef itemDef = ItemCatalog.GetItemDef(itemIndex);

                        if (itemDef && itemDef.tier == itemTier)
                        {
                            duplicateBlacklist.Add(itemIndex);
                        }
                    }
                }

                void blacklistWithoutTags(params ItemTag[] requiredTags)
                {
                    for (ItemIndex itemIndex = 0; (int)itemIndex < ItemCatalog.itemCount; itemIndex++)
                    {
                        ItemDef itemDef = ItemCatalog.GetItemDef(itemIndex);

                        bool hasAnyTag = false;
                        foreach (ItemTag itemTag in requiredTags)
                        {
                            if (itemDef.ContainsTag(itemTag))
                            {
                                hasAnyTag = true;
                                break;
                            }
                        }

                        if (!hasAnyTag)
                        {
                            duplicateBlacklist.Add(itemIndex);
                        }
                    }
                }

                // Don't duplicate itself
                blacklistItemGroup(ItemQualitiesContent.ItemQualityGroups.ShockDamageAura);

                // These items only work as "real" items (permanent, temp), adding effective count won't do anything here.
                // Add any consumable or key-like items here.
                blacklistItemGroup(ItemQualitiesContent.ItemQualityGroups.TreasureCache);
                blacklistItemGroup(ItemQualitiesContent.ItemQualityGroups.TreasureCacheVoid);
                blacklistItemGroup(ItemQualitiesContent.ItemQualityGroups.HealingPotion);
                blacklistItemGroup(ItemQualitiesContent.ItemQualityGroups.TeleportOnLowHealth);
                blacklistItemGroup(ItemQualitiesContent.ItemQualityGroups.LowerPricedChests);
                blacklistItemGroup(ItemQualitiesContent.ItemQualityGroups.ExtraLife);
                blacklistItemGroup(ItemQualitiesContent.ItemQualityGroups.ExtraLifeVoid);

                // Don't duplicate scrap
                blacklistItemTag(ItemTag.Scrap);

                // Don't duplicate any objective-related items, these are likely used as a kind of "currency" or "key" and won't work when duplicated
                blacklistItemTag(ItemTag.ObjectiveRelated);

                // No consumed items
                blacklistItemTier(ItemTier.NoTier);

                // No pages (always a consumable item)
                blacklistItemTier(ItemTier.Page);

                // no
                blacklistItemTier(ItemTier.Hex);

                // If item cannot be temporary or rental, odds are it only works as permanent, so our fake item likely won't work either
                blacklistWithoutTags(ItemTag.CanBeTemporary, ItemTag.CanBeRental);

                if (duplicateBlacklist.Count > 0)
                {
                    _duplicateBlacklist = duplicateBlacklist.ToArray();
                    Array.Sort(_duplicateBlacklist);
                }
            }
        }

        private static void SetItemIndex(On.RoR2.UI.ItemIcon.orig_SetItemIndex_SetItemIndexArgs orig, ItemIcon self, ItemIcon.SetItemIndexArgs args)
        {
            orig(self, args);

            HUD hud = self.GetComponentInParent<HUD>();
            if (!hud || !hud.targetMaster)
                return;

            if (!hud.targetMaster.TryGetComponentCached(out CharacterMasterExtraStatsTracker extraStats))
                return;

            if (extraStats.ConductorItemStacks.GetStackValue(args.newItemIndex) > 0)
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
                extraStats.ConductorItemStacks.GetNonZeroIndices(conductorItemIndices);
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

                if (Array.BinarySearch(_duplicateBlacklist, itemIndex) >= 0)
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
                Log.PatchError(il, "Failed to find ItemIndex parameter");
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
                Log.PatchError(il, "Failed to find patch location");
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
