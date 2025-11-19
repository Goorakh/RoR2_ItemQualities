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

                    if (qualityScrapIndex != ItemIndex.None && qualityConsumedScrapIndex != ItemIndex.None)
                    {
                        Inventory.ItemTransformation qualityRegenScrapTransformation = new Inventory.ItemTransformation
                        {
                            originalItemIndex = qualityConsumedScrapIndex,
                            newItemIndex = qualityScrapIndex,
                            maxToTransform = int.MaxValue,
                            transformationType = (ItemTransformationTypeIndex)CharacterMasterNotificationQueue.TransformationType.SaleStarRegen
                        };

                        qualityRegenScrapTransformation.TryTransform(self.inventory, out _);
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
                               x => x.MatchInitobj<Inventory.ItemTransformation>(),
                               x => x.MatchCallOrCallvirt<Inventory.ItemTransformation>("get_" + nameof(Inventory.ItemTransformation.originalItemIndex)),
                               x => x.MatchLdsfld(typeof(DLC1Content.Items), nameof(DLC1Content.Items.RegeneratingScrap)),
                               x => x.MatchCallOrCallvirt<Inventory.ItemTransformation>("get_" + nameof(Inventory.ItemTransformation.newItemIndex))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Goto(foundCursors[1].Next, MoveType.After); // call Inventory.ItemTransformation.get_originalItemIndex

            VariableDefinition originalItemIndexVar = il.AddVariable<ItemIndex>();
            c.Emit(OpCodes.Dup);
            c.Emit(OpCodes.Stloc, originalItemIndexVar);

            c.EmitDelegate<Func<ItemIndex, ItemIndex>>(getNonQualityItem);

            static ItemIndex getNonQualityItem(ItemIndex itemIndex)
            {
                return QualityCatalog.GetItemIndexOfQuality(itemIndex, QualityTier.None);
            }

            c.Goto(foundCursors[3].Next, MoveType.Before); // call Inventory.ItemTransformation.set_newItemIndex

            c.Emit(OpCodes.Ldloc, originalItemIndexVar);
            c.EmitDelegate<Func<ItemIndex, ItemIndex, ItemIndex>>(tryGetQualityRegeneratingScrap);

            static ItemIndex tryGetQualityRegeneratingScrap(ItemIndex consumedScrapIndex, ItemIndex originalItemIndex)
            {
                return QualityCatalog.GetItemIndexOfQuality(consumedScrapIndex, QualityCatalog.GetQualityTier(originalItemIndex));
            }
        }
    }
}
