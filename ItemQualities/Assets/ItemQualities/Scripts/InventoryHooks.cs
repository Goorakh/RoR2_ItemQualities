using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using System;
using System.Reflection;

namespace ItemQualities
{
    static class InventoryHooks
    {
        public delegate void ItemCountChangedDelegate(Inventory inventory, ItemIndex itemIndex, int itemCountDiff);

        public static event ItemCountChangedDelegate OnTempItemGivenServerGlobal;

        [SystemInitializer]
        static void Init()
        {
            IL.RoR2.Inventory.TempItemsStorage.GiveItemTempImpl.Add += GiveItemTempImpl_Add;
        }

        static void GiveItemTempImpl_Add(ILContext il)
        {
            if (!il.Method.TryFindParameter<ItemIndex>(out ParameterDefinition itemIndexParameter))
            {
                Log.Error("Failed to find ItemIndex parameter");
                return;
            }

            if (!il.Method.TryFindParameter<int>("count", out ParameterDefinition itemCountParameter))
            {
                Log.Error("Failed to find count parameter");
                return;
            }

            FieldInfo inventoryField = typeof(Inventory.TempItemsStorage.GiveItemTempImpl).GetField(nameof(Inventory.TempItemsStorage.GiveItemTempImpl.inventory), (BindingFlags)(-1));
            if (inventoryField == null || inventoryField.FieldType != typeof(Inventory))
            {
                Log.Error("Failed to find inventory field");
                return;
            }

            ILCursor c = new ILCursor(il);

            if (!c.TryGotoNext(MoveType.Before,
                               x => x.MatchRet()))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Emit(OpCodes.Ldarg_0);
            c.Emit(OpCodes.Ldfld, inventoryField);
            c.Emit(OpCodes.Ldarg, itemIndexParameter);
            c.Emit(OpCodes.Ldarg, itemCountParameter);
            c.EmitDelegate<Action<Inventory, ItemIndex, int>>(onGiveItemTemp);

            static void onGiveItemTemp(Inventory inventory, ItemIndex itemIndex, int count)
            {
                if (OnTempItemGivenServerGlobal != null)
                {
                    foreach (ItemCountChangedDelegate onTempItemGivenServerGlobal in OnTempItemGivenServerGlobal.GetInvocationList())
                    {
                        try
                        {
                            onTempItemGivenServerGlobal(inventory, itemIndex, count);
                        }
                        catch (Exception e)
                        {
                            Log.Error_NoCallerPrefix($"Caught exception in {nameof(OnTempItemGivenServerGlobal)} ({onTempItemGivenServerGlobal.Method.DeclaringType.FullName}.{onTempItemGivenServerGlobal.Method.Name}) event: {e}");
                        }
                    }
                }
            }
        }
    }
}
