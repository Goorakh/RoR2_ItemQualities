using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using System;
using System.Collections;
using System.Linq;

namespace ItemQualities
{
    internal static class InventoryRestackQualityPatch
    {
        [InitDuringStartupPhase(GameInitPhase.PostProgressBar)]
        private static void Init()
        {
            IL.RoR2.Inventory.ShrineRestackInventory += Inventory_ShrineRestackInventory;
        }

        private static void Inventory_ShrineRestackInventory(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            /*
             *  // foreach (ItemTierDef itemTierDef in ItemTierCatalog.allItemTierDefs)
             *  //                                     ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
             *  IL_0028: call      valuetype [HGCSharpUtils]HG.ReadOnlyArray`1<class RoR2.ItemTierDef> RoR2.ItemTierCatalog::get_allItemTierDefs()
             *  IL_002D: stloc.s   V_6
             *  IL_002F: ldloca.s  V_6
             *  IL_0031: call      instance valuetype [HGCSharpUtils]HG.ReadOnlyArray`1/Enumerator<!0> valuetype [HGCSharpUtils]HG.ReadOnlyArray`1<class RoR2.ItemTierDef>::GetEnumerator()
             *  IL_0036: stloc.s   V_5
             */

            VariableDefinition itemTiersReadOnlyArrayVar = null;
            VariableDefinition itemTiersEnumeratorVar = null;
            Instruction itemTierLoopBodyStartInstruction = null;
            if (!c.TryGotoNext(MoveType.Before,
                               x => x.MatchCallOrCallvirt(typeof(ItemTierCatalog), "get_" + nameof(ItemTierCatalog.allItemTierDefs)),
                               x => x.MatchStloc(il, out itemTiersReadOnlyArrayVar),
                               x => x.MatchLdloca(itemTiersReadOnlyArrayVar),
                               x => x.MatchCallOrCallvirt(out MethodReference m) && m?.Name == nameof(IEnumerable.GetEnumerator),
                               x => x.MatchStloc(il, out itemTiersEnumeratorVar),
                               x => x.MatchAny(out itemTierLoopBodyStartInstruction)))
            {
                Log.PatchError(il, "Failed to find itemTier loop");
                return;
            }

            ExceptionHandler itemTierLoopExceptionHandler = il.Body.ExceptionHandlers.FirstOrDefault(h => h.TryStart == itemTierLoopBodyStartInstruction);
            if (itemTierLoopExceptionHandler == null)
            {
                Log.PatchError(il, "Failed to find itemTier loop exception handler");
                return;
            }

            Instruction afterItemTierLoopInstruction = itemTierLoopExceptionHandler.HandlerEnd;

            VariableDefinition qualityTierVar = il.AddVariable<QualityTier>();

            {
#pragma warning disable CS0219 // Variable is assigned but its value is never used
                // Will raise a compiler error if QualityTier.None is ever not equal to -1
                const uint _Assert = (int)QualityTier.None == -1 ? 0 : -1;
#pragma warning restore CS0219 // Variable is assigned but its value is never used
            }

            // qualityTier = QualityTier.None;
            c.Emit(OpCodes.Ldc_I4_M1);
            c.Emit(OpCodes.Stloc, qualityTierVar);

            Instruction itemTierLoopStartInstruction = c.Next;

            c.Goto(afterItemTierLoopInstruction, MoveType.Before);

            c.Emit(OpCodes.Ldloca, qualityTierVar);

            // ILCursor does not retarget exception handlers, so we have to do it ourselves :)
            itemTierLoopExceptionHandler.RetargetHandlerEnd(c.Prev);

            c.EmitDelegate<IncrementQualityTierDelegate>(incrementQualityTier);
            c.Emit(OpCodes.Brtrue, itemTierLoopStartInstruction);

            static bool incrementQualityTier(ref QualityTier qualityTier)
            {
                return ++qualityTier < QualityTier.Count;
            }

            c.Goto(0);

            VariableDefinition itemTierDefVar = null;
            VariableDefinition itemDefVar = null;
            ILLabel skipItemRestackLabel = null;
            if (!c.TryGotoNext(MoveType.After,
                               x => x.MatchLdloc<ItemTierDef>(il, out itemTierDefVar),
                               x => x.MatchCallOrCallvirt<ItemTierDef>("get_" + nameof(ItemTierDef.tier)),
                               x => x.MatchLdloc<ItemDef>(il, out itemDefVar),
                               x => x.MatchCallOrCallvirt<ItemDef>("get_" + nameof(ItemDef.tier)),
                               x => x.MatchBneUn(out skipItemRestackLabel)))
            {
                Log.PatchError(il, "Failed to find item filter patch location");
                return;
            }

            c.Emit(OpCodes.Ldarg_0);
            c.Emit(OpCodes.Ldloc, itemDefVar);
            c.Emit(OpCodes.Ldloc, qualityTierVar);
            c.EmitDelegate<Func<Inventory, ItemDef, QualityTier, bool>>(canRestack);
            c.Emit(OpCodes.Brfalse, skipItemRestackLabel);

            static bool canRestack(Inventory inventory, ItemDef itemDef, QualityTier qualityTier)
            {
                ItemIndex itemIndex = itemDef.itemIndex;
                QualityTier itemQualityTier = QualityCatalog.GetQualityTier(itemIndex);

                // We are not processing this quality tier right now
                if (itemQualityTier != qualityTier)
                {
                    return false;
                }

                // Fix fake items from quality counting towards restackable items
                if (inventory.GetItemCountPermanent(itemIndex) == 0 &&
                    inventory.GetItemCountTemp(itemIndex) == 0)
                {
                    return false;
                }

                return true;
            }
        }

        private delegate bool IncrementQualityTierDelegate(ref QualityTier qualityTier);
    }
}
