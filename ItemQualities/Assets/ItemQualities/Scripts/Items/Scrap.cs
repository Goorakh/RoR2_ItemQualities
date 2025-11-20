using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using RoR2.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ItemQualities.Items
{
    static class Scrap
    {
        static ItemIndex[] _qualityScrapItemIndices = Array.Empty<ItemIndex>();

        static SceneIndex[] _convertScrapOnEntrySceneIndices = Array.Empty<SceneIndex>();

        [SystemInitializer(typeof(SceneCatalog), typeof(ItemCatalog), typeof(QualityCatalog))]
        static void Init()
        {
            HashSet<SceneIndex> convertScrapOnEntrySceneIncides = new HashSet<SceneIndex>();

            static void tryAddSceneIndexByName(string sceneName, ICollection<SceneIndex> sceneIndicesList)
            {
                SceneIndex sceneIndex = SceneCatalog.FindSceneIndex(sceneName);
                if (sceneIndex != SceneIndex.Invalid)
                {
                    sceneIndicesList.Add(sceneIndex);
                }
                else
                {
                    Log.Warning($"Failed to find scene '{sceneName}'");
                }
            }

            tryAddSceneIndexByName("moon", convertScrapOnEntrySceneIncides);
            tryAddSceneIndexByName("moon2", convertScrapOnEntrySceneIncides);

            _convertScrapOnEntrySceneIndices = convertScrapOnEntrySceneIncides.ToArray();
            Array.Sort(_convertScrapOnEntrySceneIndices);

            HashSet<ItemIndex> qualityScrapItemIndices = new HashSet<ItemIndex>();

            for (ItemIndex itemIndex = 0; (int)itemIndex < ItemCatalog.itemCount; itemIndex++)
            {
                if (QualityCatalog.GetQualityTier(itemIndex) > QualityTier.None)
                {
                    ItemDef itemDef = ItemCatalog.GetItemDef(itemIndex);
                    if (itemDef && itemDef.ContainsTag(ItemTag.Scrap))
                    {
                        qualityScrapItemIndices.Add(itemIndex);
                    }
                }
            }

            _qualityScrapItemIndices = qualityScrapItemIndices.ToArray();
            Array.Sort(_qualityScrapItemIndices);

            Stage.onServerStageBegin += onServerStageBegin;

            IL.RoR2.Projectile.TinkerProjectile.TransmuteTargetObject += ReplaceScrapFromItemDefTierQualityPatch;
            IL.RoR2.ScrapperController.BeginScrapping_UniquePickup += ReplaceScrapFromItemDefTierQualityPatch;

            On.RoR2.UI.ScrapperInfoPanelHelper.ShowInfo += ScrapperInfoPanelHelper_ShowInfo;
            IL.RoR2.UI.ScrapperInfoPanelHelper.ShowTierInfoInternal_MPButton_ItemTier_int += ScrapperInfoPanelHelper_ShowTierInfoInternal_MPButton_ItemTier_int;

            IL.RoR2.DrifterTracker.IsWhitelist += DrifterTracker_IsWhitelist;
        }

        static void onServerStageBegin(Stage stage)
        {
            SceneDef sceneDef = stage ? stage.sceneDef : null;
            SceneIndex sceneIndex = sceneDef ? sceneDef.sceneDefIndex : SceneIndex.Invalid;
            if (sceneIndex != SceneIndex.Invalid && Array.BinarySearch(_convertScrapOnEntrySceneIndices, sceneIndex) >= 0)
            {
                foreach (PlayerCharacterMasterController playerMaster in PlayerCharacterMasterController.instances)
                {
                    if (playerMaster.isConnected && playerMaster.master)
                    {
                        convertQualityScrap(playerMaster.master.inventory);
                    }
                }
            }
        }

        static void convertQualityScrap(Inventory inventory)
        {
            foreach (ItemIndex itemIndex in _qualityScrapItemIndices)
            {
                Inventory.ItemTransformation scrapTransformation = new Inventory.ItemTransformation
                {
                    originalItemIndex = itemIndex,
                    newItemIndex = QualityCatalog.GetItemIndexOfQuality(itemIndex, QualityTier.None),
                    minToTransform = 1,
                    maxToTransform = int.MaxValue,
                    allowWhenDisabled = true,
                    transformationType = (ItemTransformationTypeIndex)CharacterMasterNotificationQueue.TransformationType.Default
                };

                scrapTransformation.TryTransform(inventory, out _);
            }
        }

        static PickupIndex _scrapperPanelShowingPickupIndexContext = PickupIndex.none;

        static void ScrapperInfoPanelHelper_ShowInfo(On.RoR2.UI.ScrapperInfoPanelHelper.orig_ShowInfo orig, ScrapperInfoPanelHelper self, MPButton button, PickupDef pickupDef)
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

        static void ScrapperInfoPanelHelper_ShowTierInfoInternal_MPButton_ItemTier_int(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            int patchCount = 0;

            while (c.TryGotoNext(MoveType.After,
                                 x => x.MatchCallOrCallvirt(typeof(PickupCatalog), nameof(PickupCatalog.FindScrapIndexForItemTier))))
            {
                c.EmitDelegate<Func<PickupIndex, PickupIndex>>(getScrapPickupIndex);

                static PickupIndex getScrapPickupIndex(PickupIndex scrapPickupIndex)
                {
                    QualityTier showingPickupQualityTier = QualityCatalog.GetQualityTier(_scrapperPanelShowingPickupIndexContext);
                    if (showingPickupQualityTier > QualityCatalog.GetQualityTier(scrapPickupIndex))
                    {
                        scrapPickupIndex = QualityCatalog.GetPickupIndexOfQuality(scrapPickupIndex, showingPickupQualityTier);
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

        static void ReplaceScrapFromItemDefTierQualityPatch(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            VariableDefinition itemDefTempVar = null;

            Instruction lastMatchInstruction = null;

            int matchCount = 0;
            int patchCount = 0;

            while (c.TryGotoNext(MoveType.After,
                                 x => x.MatchCallOrCallvirt(typeof(PickupCatalog), nameof(PickupCatalog.FindScrapIndexForItemTier))))
            {
                matchCount++;

                ILCursor itemDefCursor = c.Clone();
                if (itemDefCursor.TryGotoPrev(MoveType.Before,
                                              x => x.MatchCallOrCallvirt<ItemDef>("get_" + nameof(ItemDef.tier))))
                {
                    if (lastMatchInstruction == null || itemDefCursor.IsAfter(lastMatchInstruction))
                    {
                        itemDefTempVar ??= il.AddVariable<ItemDef>();
                        itemDefCursor.EmitStoreStack(itemDefTempVar);

                        c.Emit(OpCodes.Ldloc, itemDefTempVar);
                        c.EmitDelegate<Func<PickupIndex, ItemDef, PickupIndex>>(getScrapPickupIndex);

                        static PickupIndex getScrapPickupIndex(PickupIndex scrapPickupIndex, ItemDef scrappingItem)
                        {
                            ItemIndex scrappingItemIndex = scrappingItem ? scrappingItem.itemIndex : ItemIndex.None;

                            PickupIndex qualityScrapPickupIndex = QualityCatalog.GetScrapIndexForPickup(PickupCatalog.FindPickupIndex(scrappingItemIndex));
                            return qualityScrapPickupIndex.isValid ? qualityScrapPickupIndex : scrapPickupIndex;
                        }

                        patchCount++;
                    }
                }

                lastMatchInstruction = c.Prev;
            }

            if (patchCount == 0)
            {
                Log.Error($"Failed to find patch location for {il.Method.FullName}");
            }
            else if (patchCount < matchCount)
            {
                Log.Debug($"Found {patchCount} patch location(s) (of {matchCount} possible) for {il.Method.FullName}");
            }
            else
            {
                Log.Debug($"Found all {patchCount} patch location(s) for {il.Method.FullName}");
            }
        }

        static void DrifterTracker_IsWhitelist(ILContext il)
        {
            if (!il.Method.TryFindParameter(typeof(UniquePickup).MakeByRefType(), out ParameterDefinition pickupParameter))
            {
                Log.Error("Failed to find pickup parameter");
                return;
            }

            ILCursor c = new ILCursor(il);

            if (!c.TryFindNext(out ILCursor[] foundCursors,
                               x => x.MatchLdcI4((int)ItemTag.WorldUnique),
                               x => x.MatchCallOrCallvirt<ItemDef>(nameof(ItemDef.ContainsTag))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Goto(foundCursors[1].Next, MoveType.After); // call ItemDef.ContainsTag

            c.Emit(OpCodes.Ldarg, pickupParameter);
            c.EmitDelegate<GetBaseQualityIsWorldUniqueDelegate>(getBaseQualityIsWorldUnique);

            static bool getBaseQualityIsWorldUnique(bool isWorldUnique, in UniquePickup pickup)
            {
                PickupIndex basePickupIndex = QualityCatalog.GetPickupIndexOfQuality(pickup.pickupIndex, QualityTier.None);
                if (basePickupIndex == pickup.pickupIndex)
                    return isWorldUnique;

                PickupDef basePickupDef = PickupCatalog.GetPickupDef(basePickupIndex);
                ItemDef baseItemDef = basePickupDef != null ? ItemCatalog.GetItemDef(basePickupDef.itemIndex) : null;
                if (!baseItemDef)
                    return isWorldUnique;

                return baseItemDef.ContainsTag(ItemTag.WorldUnique);
            }
        }

        delegate bool GetBaseQualityIsWorldUniqueDelegate(bool isWorldUnique, in UniquePickup pickup);
    }
}
