using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using System;

namespace ItemQualities.Items
{
    static class Scrap
    {
        [SystemInitializer]
        static void Init()
        {
            IL.RoR2.PickupPickerController.SetOptionsFromInteractor += PickupPickerController_SetOptionsFromInteractor;
        }

        static void PickupPickerController_SetOptionsFromInteractor(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (!c.TryGotoNext(MoveType.Before,
                               x => x.MatchLdfld<ItemDef>(nameof(ItemDef.canRemove))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Emit(OpCodes.Dup);
            c.Goto(c.Next, MoveType.After);
            c.EmitDelegate<Func<ItemDef, bool, bool>>(getCanRemoveForScrapper);

            static bool getCanRemoveForScrapper(ItemDef itemDef, bool canRemove)
            {
                if (canRemove)
                {
                    ItemIndex itemIndex = itemDef ? itemDef.itemIndex : ItemIndex.None;
                    QualityTier itemQualityTier = QualityCatalog.GetQualityTier(itemIndex);
                    if (itemIndex != ItemIndex.None && itemQualityTier > QualityTier.None)
                    {
                        canRemove = false;
                    }
                }

                return canRemove;
            }
        }
    }
}
