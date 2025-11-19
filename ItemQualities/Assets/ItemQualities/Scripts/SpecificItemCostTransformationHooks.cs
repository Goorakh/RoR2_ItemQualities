using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;
using RoR2;
using System;
using System.Linq;
using System.Reflection;

namespace ItemQualities
{
    static class SpecificItemCostTransformationHooks
    {
        public delegate void ModifyItemCostTransformationDelegate(ref Inventory.ItemTransformation itemTransformation, Interactor activator, int cost);
        public static event ModifyItemCostTransformationDelegate ModifyItemCostTransformation;

        static MethodInfo _getTransformationForSpecificItemCostMethod;

        [SystemInitializer]
        static void Init()
        {
            _getTransformationForSpecificItemCostMethod = typeof(CostTypeCatalog).GetMethods(BindingFlags.Static | BindingFlags.NonPublic).SingleOrDefault(m => m.Name.StartsWith("<Init>g__GetTransformationForSpecificItemCost|"));
            if (_getTransformationForSpecificItemCostMethod == null)
            {
                Log.Error("Failed to find GetTransformationForSpecificItemCost method");
                return;
            }

            MethodInfo getSpecificItemIsAffordableMethod = typeof(CostTypeCatalog).GetMethods(BindingFlags.Static | BindingFlags.NonPublic).SingleOrDefault(m => m.Name.StartsWith("<Init>g__GetSpecificItemIsAffordableMethod|"));
            if (getSpecificItemIsAffordableMethod != null)
            {
                MethodBase isAffordableMethod = getReturnedMethod(getSpecificItemIsAffordableMethod);
                if (isAffordableMethod != null)
                {
                    new ILHook(isAffordableMethod, IsAffordableHooksPatch);
                }
                else
                {
                    Log.Error($"Failed to find isAffordable method in {getSpecificItemIsAffordableMethod.DeclaringType.FullName}.{getSpecificItemIsAffordableMethod.Name}");
                }
            }
            else
            {
                Log.Error("Failed to find GetSpecificItemIsAffordableMethod method");
            }

            MethodInfo getSpecificItemPayCostMethod = typeof(CostTypeCatalog).GetMethods(BindingFlags.Static | BindingFlags.NonPublic).SingleOrDefault(m => m.Name.StartsWith("<Init>g__GetSpecificItemPayCostMethod|"));
            if (getSpecificItemPayCostMethod != null)
            {
                MethodBase payCostMethod = getReturnedMethod(getSpecificItemPayCostMethod);
                if (payCostMethod != null)
                {
                    new ILHook(payCostMethod, PayCostHooksPatch);
                }
                else
                {
                    Log.Error($"Failed to find payCost method in {getSpecificItemPayCostMethod.DeclaringType.FullName}.{getSpecificItemPayCostMethod.Name}");
                }
            }
            else
            {
                Log.Error("Failed to find GetSpecificItemPayCostMethod method");
            }
        }

        static MethodBase getReturnedMethod(MethodBase method)
        {
            using DynamicMethodDefinition dmd = new DynamicMethodDefinition(method);
            using ILContext il = new ILContext(dmd.Definition);
            ILCursor c = new ILCursor(il);

            c.Index = c.Instrs.Count - 1;
            MethodReference methodReference = null;
            if (!c.TryGotoPrev(x => x.MatchLdftn(out methodReference)))
            {
                Log.Error($"Failed to find ldftn instruction in {method.DeclaringType.FullName}.{method.Name}");
                return null;
            }

            try
            {
                return methodReference.ResolveReflection();
            }
            catch (Exception e)
            {
                Log.Error_NoCallerPrefix($"Failed to resolve method {methodReference.FullName} for {method.DeclaringType.FullName}.{method.Name}: {e}");
                return null;
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
