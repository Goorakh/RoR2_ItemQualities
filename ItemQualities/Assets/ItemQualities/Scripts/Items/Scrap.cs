using ItemQualities.Utilities.Extensions;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using RoR2.UI;
using System;
using UnityEngine;

namespace ItemQualities.Items
{
    internal static class Scrap
    {
        [SystemInitializer]
        private static void Init()
        {
            IL.RoR2.ScrapperController.BeginScrapping_UniquePickup += GenericReplaceScrapPickupPatch;

            On.RoR2.UI.ScrapperInfoPanelHelper.ShowInfo += ScrapperInfoPanelHelper_ShowInfo;
            IL.RoR2.UI.ScrapperInfoPanelHelper.ShowTierInfoInternal_MPButton_ItemTier_int += ScrapperInfoPanelHelper_ShowTierInfoInternal_MPButton_ItemTier_int;
        }

        private static bool tryGetOverrideScrapPickupIndex(PickupIndex scrappingPickupIndex, object context, out PickupIndex overrideScrapPickupIndex)
        {
            GameObject contextGameObject = null;
            if (context is GameObject)
            {
                contextGameObject = context as GameObject;
            }
            else if (context is Component)
            {
                contextGameObject = (context as Component).gameObject;
            }

            if (contextGameObject && contextGameObject.TryGetComponent(out PickupPickerPanel contextPickerPanel) && contextPickerPanel.pickerController)
            {
                contextGameObject = contextPickerPanel.pickerController.gameObject;
            }

            if (contextGameObject && contextGameObject.GetComponent<QualityScrapperController>())
            {
                overrideScrapPickupIndex = QualityCatalog.GetPickupIndexOfQuality(scrappingPickupIndex, QualityTier.None);
                return true;
            }

            if (QualityCatalog.GetQualityTier(scrappingPickupIndex) != QualityTier.None)
            {
                PickupIndex qualityScrapPickupIndex = QualityCatalog.GetScrapIndexForPickup(scrappingPickupIndex);
                if (QualityCatalog.GetQualityTier(qualityScrapPickupIndex) != QualityTier.None)
                {
                    overrideScrapPickupIndex = qualityScrapPickupIndex;
                    return true;
                }
            }

            overrideScrapPickupIndex = PickupIndex.none;
            return false;
        }

        private static PickupIndex _scrapperPanelShowingPickupIndexContext = PickupIndex.none;

        private static void ScrapperInfoPanelHelper_ShowInfo(On.RoR2.UI.ScrapperInfoPanelHelper.orig_ShowInfo orig, ScrapperInfoPanelHelper self, MPButton button, PickupDef pickupDef)
        {
            if (pickupDef != null)
            {
                _scrapperPanelShowingPickupIndexContext = pickupDef.pickupIndex;
            }

            try
            {
                orig(self, button, pickupDef);
            }
            finally
            {
                _scrapperPanelShowingPickupIndexContext = PickupIndex.none;
            }
        }

        private static void ScrapperInfoPanelHelper_ShowTierInfoInternal_MPButton_ItemTier_int(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            int patchCount = 0;

            while (c.TryGotoNext(MoveType.After,
                                 x => x.MatchCallOrCallvirt(typeof(PickupCatalog), nameof(PickupCatalog.FindScrapIndexForItemTier))))
            {
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate<Func<PickupIndex, ScrapperInfoPanelHelper, PickupIndex>>(getScrapPickupIndexLocal);

                static PickupIndex getScrapPickupIndexLocal(PickupIndex scrapPickupIndex, ScrapperInfoPanelHelper context)
                {
                    PickupIndex scrappingPickupIndex = _scrapperPanelShowingPickupIndexContext;
                    if (tryGetOverrideScrapPickupIndex(scrappingPickupIndex, context, out PickupIndex overrideScrapPickupIndex))
                    {
                        return overrideScrapPickupIndex;
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

        public static void GenericReplaceScrapPickupPatch(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            int patchCount = 0;

            VariableDefinition itemDefVar = null;
            while (c.TryGotoNext(MoveType.After,
                                 x => x.MatchLdloc<ItemDef>(il, out itemDefVar),
                                 x => x.MatchCallOrCallvirt<ItemDef>("get_" + nameof(ItemDef.tier)),
                                 x => x.MatchCallOrCallvirt(typeof(PickupCatalog), nameof(PickupCatalog.FindScrapIndexForItemTier))))
            {
                c.Emit(OpCodes.Ldloc, itemDefVar);
                c.Emit(il.Method.Parameters.Count > 0 ? OpCodes.Ldarg_0 : OpCodes.Ldnull);
                c.EmitDelegate<Func<PickupIndex, ItemDef, object, PickupIndex>>(getScrapPickupIndexLocal);
                static PickupIndex getScrapPickupIndexLocal(PickupIndex scrapPickupIndex, ItemDef scrappingItem, object context)
                {
                    ItemIndex scrappingItemIndex = scrappingItem ? scrappingItem.itemIndex : ItemIndex.None;
                    PickupIndex scrappingPickupIndex = PickupCatalog.FindPickupIndex(scrappingItemIndex);
                    if (tryGetOverrideScrapPickupIndex(scrappingPickupIndex, context, out PickupIndex overrideScrapPickupIndex))
                    {
                        return overrideScrapPickupIndex;
                    }

                    return scrapPickupIndex;
                }

                patchCount++;
            }

            if (patchCount == 0)
            {
                Log.Error($"Failed to find patch location for {il.Method.FullName}");
            }
            else
            {
                Log.Debug($"Found {patchCount} patch location(s) for {il.Method.FullName}");
            }
        }
    }
}
