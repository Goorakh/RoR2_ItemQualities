using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
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
            IL.EntityStates.Scrapper.ScrappingToIdle.OnEnter += ScrappingToIdle_OnEnter;
            IL.RoR2.UI.ScrapperInfoPanelHelper.ShowInfo += ScrapperInfoPanelHelper_ShowInfo;
        }

        static void ScrappingToIdle_OnEnter(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            int patchCount = 0;

            VariableDefinition scrappingItemDefVar = il.AddVariable<ItemDef>();

            while (c.TryGotoNext(MoveType.Before,
                                 x => x.MatchCallOrCallvirt(typeof(PickupCatalog), nameof(PickupCatalog.FindScrapIndexForItemTier))))
            {
                Instruction findScrapCallInstruction = c.Next;

                if (c.TryGotoPrev(MoveType.After,
                                  x => x.MatchCallOrCallvirt(typeof(ItemCatalog), nameof(ItemCatalog.GetItemDef))))
                {
                    c.MoveAfterLabels();
                    c.EmitStoreStack(scrappingItemDefVar);

                    c.Goto(findScrapCallInstruction, MoveType.After);
                    c.MoveAfterLabels();

                    c.Emit(OpCodes.Ldloc, scrappingItemDefVar);
                    c.EmitDelegate<Func<PickupIndex, ItemDef, PickupIndex>>(tryGetQualityScrapPickupIndex);

                    static PickupIndex tryGetQualityScrapPickupIndex(PickupIndex scrapPickupIndex, ItemDef scrappingItemDef)
                    {
                        ItemIndex scrappingItemIndex = scrappingItemDef ? scrappingItemDef.itemIndex : ItemIndex.None;
                        if (scrappingItemIndex != ItemIndex.None)
                        {
                            PickupIndex qualityScrapPickupIndex = QualityCatalog.GetScrapIndexForPickup(PickupCatalog.FindPickupIndex(scrappingItemIndex));
                            if (qualityScrapPickupIndex != PickupIndex.none)
                            {
                                scrapPickupIndex = qualityScrapPickupIndex;
                            }
                        }

                        return scrapPickupIndex;
                    }
                }
                else
                {
                    c.Goto(findScrapCallInstruction, MoveType.After);
                }

                patchCount++;
            }

            if (patchCount == 0)
            {
                Log.Error("Failed to find patch location");
            }
            else
            {
                Log.Debug($"Found {patchCount} patch location(s)");
            }
        }

        static void ScrapperInfoPanelHelper_ShowInfo(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (!il.Method.TryFindParameter<PickupDef>(out ParameterDefinition pickupDefParameter))
            {
                Log.Error("Failed to find PickupDef parameter");
                return;
            }

            int patchCount = 0;

            while (c.TryGotoNext(MoveType.After,
                                 x => x.MatchCallOrCallvirt(typeof(PickupCatalog), nameof(PickupCatalog.FindScrapIndexForItemTier))))
            {
                c.Emit(OpCodes.Ldarg, pickupDefParameter);
                c.EmitDelegate<Func<PickupIndex, PickupDef, PickupIndex>>(tryGetQualityScrapPickupIndex);

                static PickupIndex tryGetQualityScrapPickupIndex(PickupIndex scrapPickupIndex, PickupDef scrappingPickupDef)
                {
                    PickupIndex scrappingPickupIndex = scrappingPickupDef != null ? scrappingPickupDef.pickupIndex : PickupIndex.none;
                    if (scrappingPickupIndex != PickupIndex.none)
                    {
                        PickupIndex qualityScrapPickupIndex = QualityCatalog.GetScrapIndexForPickup(scrappingPickupIndex);
                        if (qualityScrapPickupIndex != PickupIndex.none)
                        {
                            scrapPickupIndex = qualityScrapPickupIndex;
                        }
                    }

                    return scrapPickupIndex;
                }

                patchCount++;
            }

            if (patchCount == 0)
            {
                Log.Error("Failed to find patch location");
            }
            else
            {
                Log.Debug($"Found {patchCount} patch location(s)");
            }
        }
    }
}
