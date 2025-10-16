using ItemQualities.Items;
using MonoMod.Cil;
using RoR2;
using RoR2.Items;

namespace ItemQualities
{
    static class ContagiousItemQualityHandler
    {
        [SystemInitializer]
        static void Init()
        {
            IL.RoR2.Items.ContagiousItemManager.OnInventoryChangedGlobal += ContagiousItemManager_OnInventoryChangedGlobal;
            IL.RoR2.Items.ContagiousItemManager.StepInventoryInfection += ContagiousItemManager_StepInventoryInfection;
        }

        static void ContagiousItemManager_OnInventoryChangedGlobal(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (!c.TryFindNext(out ILCursor[] foundCursors,
                               x => x.MatchLdsfld(typeof(ContagiousItemManager), nameof(ContagiousItemManager._transformationInfos)),
                               x => x.MatchCallOrCallvirt<Inventory>(nameof(Inventory.GetItemCount))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Goto(foundCursors[1].Next, MoveType.Before); // call Inventory.GetItemCount
            ItemHooks.EmitSingleCombineGroupedItemCounts(c);
        }

        static void ContagiousItemManager_StepInventoryInfection(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (!c.TryFindNext(out ILCursor[] foundCursors,
                               x => x.MatchLdsfld(typeof(ContagiousItemManager), nameof(ContagiousItemManager.originalToTransformed)),
                               x => x.MatchCallOrCallvirt<Inventory>(nameof(Inventory.GetItemCount))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Goto(foundCursors[1].Next, MoveType.Before); // call Inventory.GetItemCount
            ItemHooks.EmitSingleCombineGroupedItemCounts(c);
        }
    }
}
