using MonoMod.Cil;
using RoR2;
using RoR2.Items;
using System;

namespace ItemQualities
{
    internal static class ContagiousItemQualityHandler
    {
        [SystemInitializer]
        private static void Init()
        {
            IL.RoR2.Items.ContagiousItemManager.OnInventoryChangedGlobal += ContagiousItemManager_OnInventoryChangedGlobal;
            IL.RoR2.Items.ContagiousItemManager.StepInventoryInfection += ContagiousItemManager_StepInventoryInfection;
        }

        private static ItemIndex getItemForTransformation(ItemIndex itemIndex)
        {
            return QualityCatalog.GetItemIndexOfQuality(itemIndex, QualityTier.None);
        }

        private static void ContagiousItemManager_OnInventoryChangedGlobal(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (!c.TryFindNext(out ILCursor[] foundCursors,
                               x => x.MatchLdsfld(typeof(ContagiousItemManager), nameof(ContagiousItemManager._transformationInfos)),
                               x => x.MatchCallOrCallvirt<Inventory>(nameof(Inventory.GetItemCountEffective))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Goto(foundCursors[1].Next, MoveType.Before); // call Inventory.GetItemCountEffective
            c.EmitDelegate<Func<ItemIndex, ItemIndex>>(getItemForTransformation);
        }

        private static void ContagiousItemManager_StepInventoryInfection(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (!c.TryFindNext(out ILCursor[] foundCursors,
                               x => x.MatchLdsfld(typeof(ContagiousItemManager), nameof(ContagiousItemManager.originalToTransformed)),
                               x => x.MatchCallOrCallvirt<Inventory>(nameof(Inventory.GetItemCountEffective))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Goto(foundCursors[1].Next, MoveType.Before); // call Inventory.GetItemCountEffective
            c.EmitDelegate<Func<ItemIndex, ItemIndex>>(getItemForTransformation);
        }
    }
}
