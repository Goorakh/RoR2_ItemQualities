using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;
using RoR2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ItemQualities
{
    static class SpecificItemCostTransformationHooks
    {
        public delegate void ModifyItemCostTransformationDelegate(ref Inventory.ItemTransformation itemTransformation, Interactor activator, int cost);
        public static event ModifyItemCostTransformationDelegate ModifyItemCostTransformation;

        static MethodInfo _getTransformationForSpecificItemCostMethod;

        [SystemInitializer(typeof(CostTypeCatalog))]
        static void Init()
        {
            _getTransformationForSpecificItemCostMethod = typeof(CostTypeCatalog).GetMethods(BindingFlags.Static | BindingFlags.NonPublic).SingleOrDefault(m => m.Name.StartsWith("<Init>g__GetTransformationForSpecificItemCost|"));
            if (_getTransformationForSpecificItemCostMethod == null)
            {
                Log.Error("Failed to find GetTransformationForSpecificItemCost method");
                return;
            }

            HashSet<MethodInfo> patchedMethods = new HashSet<MethodInfo>();

            Span<CostTypeIndex> specificItemCostTypeIndices = stackalloc CostTypeIndex[]
            {
                CostTypeIndex.ArtifactShellKillerItem,
                CostTypeIndex.TreasureCacheItem,
                CostTypeIndex.TreasureCacheVoidItem
            };

            int foundIsAffordableMethodCount = 0;
            int foundPayCostMethodCount = 0;

            foreach (CostTypeIndex costTypeIndex in specificItemCostTypeIndices)
            {
                CostTypeDef costTypeDef = CostTypeCatalog.GetCostTypeDef(costTypeIndex);
                if (costTypeDef != null)
                {
                    MethodInfo isAffordableMethod = costTypeDef.isAffordable?.Method;
                    if (isAffordableMethod != null && patchedMethods.Add(isAffordableMethod))
                    {
                        foundIsAffordableMethodCount++;
                        new ILHook(isAffordableMethod, IsAffordableHooksPatch);
                    }

                    MethodInfo payCostMethod = costTypeDef.payCost?.Method;
                    if (payCostMethod != null && patchedMethods.Add(payCostMethod))
                    {
                        foundPayCostMethodCount++;
                        new ILHook(payCostMethod, PayCostHooksPatch);
                    }
                }
            }

            if (foundIsAffordableMethodCount == 0)
            {
                Log.Error("Failed to find specific item isAffordable method");
            }
            else
            {
                Log.Debug($"Found {foundIsAffordableMethodCount} specific item isAffordable method(s)");
            }

            if (foundPayCostMethodCount == 0)
            {
                Log.Error("Failed to find specific item payCost method");
            }
            else
            {
                Log.Debug($"Found {foundPayCostMethodCount} specific item payCost method(s)");
            }
        }

        static void IsAffordableHooksPatch(ILContext il)
        {
            if (!il.Method.TryFindParameter<CostTypeDef.IsAffordableContext>(out ParameterDefinition contextParameter))
            {
                Log.Error("Failed to find IsAffordableContext parameter");
                return;
            }

            ILCursor c = new ILCursor(il);

            if (!c.TryGotoNext(MoveType.After,
                               x => x.MatchCallOrCallvirt(_getTransformationForSpecificItemCostMethod)))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Emit(OpCodes.Ldarg, contextParameter);
            c.EmitDelegate<Func<Inventory.ItemTransformation, CostTypeDef.IsAffordableContext, Inventory.ItemTransformation>>(getItemTransformation);

            static Inventory.ItemTransformation getItemTransformation(Inventory.ItemTransformation itemTransformation, CostTypeDef.IsAffordableContext context)
            {
                ModifyItemCostTransformation?.Invoke(ref itemTransformation, context.activator, context.cost);
                return itemTransformation;
            }
        }

        static void PayCostHooksPatch(ILContext il)
        {
            if (!il.Method.TryFindParameter<CostTypeDef.PayCostContext>(out ParameterDefinition contextParameter))
            {
                Log.Error("Failed to find IsAffordableContext parameter");
                return;
            }

            ILCursor c = new ILCursor(il);

            if (!c.TryGotoNext(MoveType.After,
                               x => x.MatchCallOrCallvirt(_getTransformationForSpecificItemCostMethod)))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Emit(OpCodes.Ldarg, contextParameter);
            c.EmitDelegate<Func<Inventory.ItemTransformation, CostTypeDef.PayCostContext, Inventory.ItemTransformation>>(getItemTransformation);

            static Inventory.ItemTransformation getItemTransformation(Inventory.ItemTransformation itemTransformation, CostTypeDef.PayCostContext context)
            {
                ModifyItemCostTransformation?.Invoke(ref itemTransformation, context.activator, context.cost);
                return itemTransformation;
            }
        }
    }
}
