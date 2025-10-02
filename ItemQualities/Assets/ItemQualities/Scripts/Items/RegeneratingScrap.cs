using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using RoR2;
using System;
using System.Reflection;

namespace ItemQualities.Items
{
    static class RegeneratingScrap
    {
        [SystemInitializer(typeof(CostTypeCatalog))]
        static void Init()
        {
            On.RoR2.CharacterMaster.TryRegenerateScrap += CharacterMaster_TryRegenerateScrap;

            CostTypeDef greenItemCostDef = CostTypeCatalog.GetCostTypeDef(CostTypeIndex.GreenItem);
            MethodInfo greenItemPayCostMethod = greenItemCostDef?.payCost?.Method;
            if (greenItemPayCostMethod != null)
            {
                new ILHook(greenItemPayCostMethod, ItemPayCostManipulator);
            }
            else
            {
                Log.Error($"Failed to find item PayCost method");
            }
        }

        static void CharacterMaster_TryRegenerateScrap(On.RoR2.CharacterMaster.orig_TryRegenerateScrap orig, CharacterMaster self)
        {
            orig(self);

            if (self && self.inventory)
            {
                for (QualityTier qualityTier = 0; qualityTier < QualityTier.Count; qualityTier++)
                {
                    ItemIndex qualityScrapIndex = ItemQualitiesContent.ItemQualityGroups.RegeneratingScrap.GetItemIndex(qualityTier);
                    ItemIndex qualityConsumedScrapIndex = ItemQualitiesContent.ItemQualityGroups.RegeneratingScrapConsumed.GetItemIndex(qualityTier);

                    int qualityScrapCount = self.inventory.GetItemCount(qualityConsumedScrapIndex);
                    if (qualityScrapCount > 0)
                    {
                        self.inventory.RemoveItem(qualityConsumedScrapIndex, qualityScrapCount);
                        self.inventory.GiveItem(qualityScrapIndex, qualityScrapCount);

                        CharacterMasterNotificationQueue.SendTransformNotification(self, qualityConsumedScrapIndex, qualityScrapIndex, CharacterMasterNotificationQueue.TransformationType.RegeneratingScrapRegen);
                    }
                }
            }
        }

        static void ItemPayCostManipulator(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (!il.Method.TryFindParameter<CostTypeDef.PayCostContext>(out ParameterDefinition contextParameter))
            {
                Log.Error("Failed to find context parameter");
                return;
            }

            if (!c.TryFindNext(out ILCursor[] foundCursors,
                               x => x.MatchLdfld<CostTypeDef.PayCostResults>(nameof(CostTypeDef.PayCostResults.itemsTaken)),
                               x => x.MatchLdsfld(typeof(DLC1Content.Items), nameof(DLC1Content.Items.RegeneratingScrap)),
                               x => x.MatchBneUn(out _),
                               x => x.MatchLdsfld(typeof(DLC1Content.Items), nameof(DLC1Content.Items.RegeneratingScrapConsumed)),
                               x => x.MatchCallOrCallvirt(typeof(CharacterMasterNotificationQueue), nameof(CharacterMasterNotificationQueue.SendTransformNotification))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Goto(foundCursors[1].Next, MoveType.Before); // ldsfld DLC1Content.Items.RegeneratingScrap

            int itemTakenLocalIndex = -1;
            if (!c.TryGotoPrev(MoveType.After, x => x.MatchLdloc(typeof(ItemIndex), il, out itemTakenLocalIndex)))
            {
                Log.Error("Failed to find taken item local index");
                return;
            }

            c.EmitDelegate<Func<ItemIndex, ItemIndex>>(getTakenNonQualityItem);

            static ItemIndex getTakenNonQualityItem(ItemIndex itemIndex)
            {
                return QualityCatalog.GetItemIndexOfQuality(itemIndex, QualityTier.None);
            }

            c.Goto(foundCursors[3].Next, MoveType.After); // ldsfld RegeneratingScrapConsumed

            c.Emit(OpCodes.Ldloc, itemTakenLocalIndex);
            c.EmitDelegate<Func<ItemDef, ItemIndex, ItemDef>>(getQualityRegeneratingScrapConsumed);

            static ItemDef getQualityRegeneratingScrapConsumed(ItemDef regenScrapConsumed, ItemIndex takenItemIndex)
            {
                QualityTier qualityTier = QualityCatalog.GetQualityTier(takenItemIndex);

                ItemIndex qualityRegenScrapConsumedItemIndex = QualityCatalog.GetItemIndexOfQuality(regenScrapConsumed ? regenScrapConsumed.itemIndex : ItemIndex.None, qualityTier);
                ItemDef qualityRegenScrapConsumed = ItemCatalog.GetItemDef(qualityRegenScrapConsumedItemIndex);

                if (qualityRegenScrapConsumed)
                {
                    regenScrapConsumed = qualityRegenScrapConsumed;
                }

                return regenScrapConsumed;
            }

            c.Goto(foundCursors[4].Next, MoveType.Before); // call CharacterMasterNotificationQueue.SendTransformNotification

            c.Emit(OpCodes.Ldarg, contextParameter);
            c.EmitDelegate<Func<CostTypeDef.PayCostContext, bool>>(tookAnyNonQualityRegeneratingScrap);
            c.EmitSkipMethodCall(OpCodes.Brfalse);

            static bool tookAnyNonQualityRegeneratingScrap(CostTypeDef.PayCostContext context)
            {
                if (context.results != null)
                {
                    foreach (ItemIndex takenItemIndex in context.results.itemsTaken)
                    {
                        if (takenItemIndex == DLC1Content.Items.RegeneratingScrap.itemIndex)
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            c.Emit(OpCodes.Ldarg, contextParameter);
            c.EmitDelegate<Action<CostTypeDef.PayCostContext>>(handleQualityRegeneratingScrap);

            static void handleQualityRegeneratingScrap(CostTypeDef.PayCostContext context)
            {
                if (!context.activatorMaster || context.results == null)
                    return;

                bool[] hasTakenQualityTierScrap = new bool[(int)QualityTier.Count];

                foreach (ItemIndex takenItemIndex in context.results.itemsTaken)
                {
                    QualityTier takenQualityTier = QualityCatalog.GetQualityTier(takenItemIndex);
                    if (takenQualityTier <= QualityTier.None)
                        continue;

                    ItemQualityGroup takenItemGroup = QualityCatalog.GetItemQualityGroup(QualityCatalog.FindItemQualityGroupIndex(takenItemIndex));
                    if (!takenItemGroup || takenItemGroup.BaseItemIndex != DLC1Content.Items.RegeneratingScrap.itemIndex)
                        continue;

                    hasTakenQualityTierScrap[(int)takenQualityTier] = true;
                }

                for (QualityTier qualityTier = 0; qualityTier < QualityTier.Count; qualityTier++)
                {
                    bool hasTakenScrapOfTier = hasTakenQualityTierScrap[(int)qualityTier];
                    if (hasTakenScrapOfTier)
                    {
                        ItemIndex qualityScrapIndex = ItemQualitiesContent.ItemQualityGroups.RegeneratingScrap.GetItemIndex(qualityTier);
                        ItemIndex qualityConsumedScrapIndex = ItemQualitiesContent.ItemQualityGroups.RegeneratingScrapConsumed.GetItemIndex(qualityTier);

                        CharacterMasterNotificationQueue.SendTransformNotification(context.activatorMaster, qualityScrapIndex, qualityConsumedScrapIndex, CharacterMasterNotificationQueue.TransformationType.Default);
                    }
                }
            }
        }
    }
}
