using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using RoR2.UI;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace ItemQualities.Items
{
    public static class ShockDamageAura
    {
        [SystemInitializer]
        static void Init()
        {
            IL.RoR2.Inventory.UpdateEffectiveItemStacks += UpdateEffectiveItemStacks;
            On.RoR2.Inventory.CalculateEffectiveItemStacks += CalculateEffectiveItemStacks;
            On.RoR2.Inventory.HandleInventoryChanged += HandleInventoryChanged;
            On.RoR2.UI.ItemIcon.SetItemIndex_ItemIndex_int_float += SetItemIndex;
        }

        private static void SetItemIndex(On.RoR2.UI.ItemIcon.orig_SetItemIndex_ItemIndex_int_float orig, ItemIcon self, ItemIndex newItemIndex, int newItemCount, float newDurationPercent)
        {
            orig(self, newItemIndex, newItemCount, newDurationPercent);
            Transform parent = self.transform.parent;
            if (!parent)
                return;
            ItemInventoryDisplay itemInventoryDisplay = parent.GetComponent<ItemInventoryDisplay>();
            if (!itemInventoryDisplay)
                return;
            CharacterBody body = itemInventoryDisplay._characterBody;
            if (!body || !body.master)
                return;
            CharacterMasterExtraStatsTracker extraStats = body.master.GetComponent<CharacterMasterExtraStatsTracker>();
            if (!extraStats)
                return;

            if (extraStats.ConductorItemStacks.GetStackValue(newItemIndex) > 0)
            {
                self.spriteAsNumberManager.SetSpriteColor(ColorCatalog.GetColor(ColorCatalog.ColorIndex.BossItem));
            } else {
                self.spriteAsNumberManager.SetSpriteColor(Color.white);
            }
        }

        private static void OnDeserialize(On.RoR2.Inventory.orig_OnDeserialize orig, Inventory self, NetworkReader reader, bool initialState)
        {
            orig(self, reader, initialState);
            if (reader.ReadBoolean())
            {
                CharacterMasterExtraStatsTracker extraStats = self.GetComponent<CharacterMasterExtraStatsTracker>();
                extraStats.ConductorItemStacks.Deserialize(reader);
            }
        }

        private static bool OnSerialize(On.RoR2.Inventory.orig_OnSerialize orig, Inventory self, NetworkWriter writer, bool initialState)
        {
            bool result = orig(self, writer, initialState);
            CharacterMasterExtraStatsTracker extraStats = self.GetComponent<CharacterMasterExtraStatsTracker>();
            if (!extraStats)
            {
                writer.Write(false);
                return result;
            }
            writer.Write(true);
            extraStats.ConductorItemStacks.Serialize(writer);
            return result;
        }

        private static void HandleInventoryChanged(On.RoR2.Inventory.orig_HandleInventoryChanged orig, Inventory self)
        {
            orig(self);
            CharacterMasterExtraStatsTracker extraStats = self.GetComponent<CharacterMasterExtraStatsTracker>();
            if (!extraStats)
                return;
            int? conductorIndex = null;
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
            if (conductorIndex == null)
                return;

            ItemQualityCounts shockDamageAura = self.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.ShockDamageAura);
            int itemBuffCount = shockDamageAura.HighestQuality switch
            {
                QualityTier.Uncommon => 2,
                QualityTier.Rare => 3,
                QualityTier.Epic => 4,
                QualityTier.Legendary => 5,
                _ => 0,
            };

            int extraStacks =   shockDamageAura.UncommonCount * 2 +
                                shockDamageAura.RareCount * 3 +
                                shockDamageAura.EpicCount * 4 +
                                shockDamageAura.LegendaryCount * 5;

            extraStats.ConductorItemStacks.Clear();
            for (int i = 0; i < itemBuffCount; i++)
            {
                if (i >= itemAcquisitionOrder.Count)
                    break;

                int index = (conductorIndex.Value - i - 1);
                if (index < 0)
                {
                    index += itemAcquisitionOrder.Count;
                }

                ItemQualityGroupIndex shockDamageAuraGroup = QualityCatalog.FindItemQualityGroupIndex(itemAcquisitionOrder[index]);
                if (shockDamageAuraGroup == ItemQualitiesContent.ItemQualityGroups.ShockDamageAura.GroupIndex ||
                shockDamageAuraGroup == ItemQualitiesContent.ItemQualityGroups.ScrapWhite.GroupIndex ||
                shockDamageAuraGroup == ItemQualitiesContent.ItemQualityGroups.ScrapGreen.GroupIndex ||
                shockDamageAuraGroup == ItemQualitiesContent.ItemQualityGroups.ScrapRed.GroupIndex ||
                shockDamageAuraGroup == ItemQualitiesContent.ItemQualityGroups.ScrapYellow.GroupIndex ||
                shockDamageAuraGroup == ItemQualitiesContent.ItemQualityGroups.RegeneratingScrap.GroupIndex ||
                ItemCatalog.GetItemDef(itemAcquisitionOrder[index]).tier == ItemTier.NoTier)
                {
                    itemBuffCount++;
                    continue;
                }

                extraStats.ConductorItemStacks.SetStackValue(itemAcquisitionOrder[index], extraStacks);
                self.UpdateEffectiveItemStacks(itemAcquisitionOrder[index]);
            }

            foreach (HUD hud in HUD.instancesList)
            {
                hud.itemInventoryDisplay.OnInventoryChanged();
            }
        }

        private static int CalculateEffectiveItemStacks(On.RoR2.Inventory.orig_CalculateEffectiveItemStacks orig, Inventory self, ItemIndex itemIndex)
        {
            int result = orig(self, itemIndex);

            if (self.TryGetComponent(out CharacterMasterExtraStatsTracker extraStats))
            {
                result += extraStats.ConductorItemStacks.GetStackValue(itemIndex);
            }
            return Math.Clamp(result, 0, int.MaxValue);
        }

        static void UpdateEffectiveItemStacks(ILContext il)
        {
            if (!il.Method.TryFindParameter<ItemIndex>(out ParameterDefinition ItemIndexParameter))
            {
                Log.Error("Failed to find ItemIndex parameter");
                return;
            }

            ILCursor c = new ILCursor(il);
            int hasItemIndexFlagLoc = 0;
            int stackNumLoc = 0;

            if (c.TryGotoNext(MoveType.After,
                    x => x.MatchLdcI4(0),
                    x => x.MatchStloc(out hasItemIndexFlagLoc),
                    x => x.MatchLdcI4(0),
                    x => x.MatchStloc(out stackNumLoc)
                ))
            {
                c.Emit(OpCodes.Ldarg_0);
                c.Emit(OpCodes.Ldloc, stackNumLoc);
                c.Emit(OpCodes.Ldarg, ItemIndexParameter);
                c.EmitDelegate<Func<Inventory, int, ItemIndex, int>>(addConductorStacks);
                c.Emit(OpCodes.Stloc, stackNumLoc);
            }
            else
            {
                Log.Error("IL Hook failed!");
                return;
            }

            int addConductorStacks(Inventory self, int stackNum, ItemIndex itemIndex)
            {
                if (self.TryGetComponent(out CharacterMasterExtraStatsTracker extraStats))
                {
                    return stackNum + extraStats.ConductorItemStacks.GetStackValue(itemIndex);
                }
                return stackNum;
            }
        }
    }
}
