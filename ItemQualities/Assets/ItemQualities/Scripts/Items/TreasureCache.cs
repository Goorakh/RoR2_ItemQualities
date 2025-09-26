using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using RoR2;
using System;
using System.Reflection;
using UnityEngine;

namespace ItemQualities.Items
{
    static class TreasureCache
    {
        [SystemInitializer(typeof(CostTypeCatalog))]
        static void Init()
        {
            CostTypeDef keyCostDef = CostTypeCatalog.GetCostTypeDef(CostTypeIndex.TreasureCacheItem);

            MethodInfo keyCostIsAffordableMethod = keyCostDef?.isAffordable?.Method;
            if (keyCostIsAffordableMethod != null)
            {
                new ILHook(keyCostIsAffordableMethod, ItemHooks.CombineGroupedItemCountsPatch);
            }
            else
            {
                Log.Error("Failed to find Key IsAffordable method");
            }

            MethodInfo keyPayCostMethod = keyCostDef?.payCost?.Method;
            if (keyPayCostMethod != null)
            {
                new ILHook(keyPayCostMethod, KeyCostDef_PayCost);
            }
            else
            {
                Log.Error("Failed to find Key PayCost method");
            }
        }

        static void KeyCostDef_PayCost(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (!il.Method.TryFindParameter<CostTypeDef.PayCostContext>(out ParameterDefinition contextParameter))
            {
                Log.Error("Failed to find PayCostContext parameter");
                return;
            }

            c.Emit(OpCodes.Ldarga, contextParameter);
            c.EmitDelegate<TryPayQualityKeysDelegate>(tryPayQualityKeys);
            static int tryPayQualityKeys(ref CostTypeDef.PayCostContext context)
            {
                CharacterBody body = context.activatorBody;
                Inventory inventory = body ? body.inventory : null;
                if (!inventory)
                    return 0;

                int totalQualityKeysSpent = 0;

                for (QualityTier qualityTier = QualityTier.Count - 1; qualityTier > QualityTier.None; qualityTier--)
                {
                    ItemIndex qualityKeyItemIndex = ItemQualitiesContent.ItemQualityGroups.TreasureCache.GetItemIndex(qualityTier);
                    int qualityKeyItemCount = inventory.GetItemCount(qualityKeyItemIndex);
                    int keysOfQualityToSpend = Mathf.Min(context.cost - totalQualityKeysSpent, qualityKeyItemCount);
                    if (keysOfQualityToSpend > 0)
                    {
                        inventory.RemoveItem(qualityKeyItemIndex, keysOfQualityToSpend);

                        for (int i = 0; i < keysOfQualityToSpend; i++)
                        {
                            context.results.itemsTaken.Add(qualityKeyItemIndex);
                        }

                        totalQualityKeysSpent += keysOfQualityToSpend;

                        if (totalQualityKeysSpent >= context.cost)
                            break;
                    }
                }

                return totalQualityKeysSpent;
            }

            VariableDefinition numQualityKeysPaidVar = il.AddVariable<int>();
            c.Emit(OpCodes.Stloc, numQualityKeysPaidVar);

            if (!c.TryGotoNext(MoveType.Before,
                               x => x.MatchCallOrCallvirt<Inventory>(nameof(Inventory.RemoveItem))))
            {
                Log.Error("Failed to find key item remove patch location");
                return;
            }

            c.Emit(OpCodes.Ldloc, numQualityKeysPaidVar);
            c.EmitDelegate<Func<int, int, int>>(getRemainingNonQualityCost);
            static int getRemainingNonQualityCost(int totalKeysCost, int numQualityKeysPaid)
            {
                return Mathf.Max(0, totalKeysCost - numQualityKeysPaid);
            }

            c.Emit(OpCodes.Dup);

            VariableDefinition numNonQualityKeysPaidVar = il.AddVariable<int>();
            c.Emit(OpCodes.Stloc, numNonQualityKeysPaidVar);

            c.Index++;

            c.Emit(OpCodes.Ldloc, numNonQualityKeysPaidVar);
            c.Emit(OpCodes.Ldarga, contextParameter);
            c.EmitDelegate<OnNonQualityKeysPaidDelegate>(onNonQualityKeysPaid);
            static void onNonQualityKeysPaid(int numNonQualityKeysPaid, ref CostTypeDef.PayCostContext context)
            {
                for (int i = 0; i < numNonQualityKeysPaid; i++)
                {
                    context.results.itemsTaken.Add(RoR2Content.Items.TreasureCache.itemIndex);
                }
            }
        }

        delegate int TryPayQualityKeysDelegate(ref CostTypeDef.PayCostContext context);
        delegate void OnNonQualityKeysPaidDelegate(int numNonQualityKeysPaid, ref CostTypeDef.PayCostContext context);
    }
}
