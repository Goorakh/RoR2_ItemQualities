using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ItemQualities.Items
{
    static class Scrap
    {
        static SceneIndex[] _convertScrapOnEntrySceneIndices = Array.Empty<SceneIndex>();

        [SystemInitializer(typeof(SceneCatalog))]
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

            IL.EntityStates.Scrapper.ScrappingToIdle.OnEnter += ScrappingToIdle_OnEnter;
            IL.RoR2.UI.ScrapperInfoPanelHelper.ShowInfo += ScrapperInfoPanelHelper_ShowInfo;

            Stage.onServerStageBegin += onServerStageBegin;
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
            for (ItemIndex itemIndex = 0; (int)itemIndex < ItemCatalog.itemCount; itemIndex++)
            {
                ItemDef itemDef = ItemCatalog.GetItemDef(itemIndex);
                if (itemDef && itemDef.ContainsTag(ItemTag.Scrap))
                {
                    ItemIndex baseItemIndex = QualityCatalog.GetItemIndexOfQuality(itemIndex, QualityTier.None);
                    if (baseItemIndex != ItemIndex.None && baseItemIndex != itemIndex)
                    {
                        Inventory.ItemTransformation scrapTransformation = new Inventory.ItemTransformation
                        {
                            originalItemIndex = itemIndex,
                            newItemIndex = baseItemIndex,
                            minToTransform = 1,
                            maxToTransform = int.MaxValue,
                            allowWhenDisabled = true,
                            transformationType = (ItemTransformationTypeIndex)CharacterMasterNotificationQueue.TransformationType.Default
                        };

                        scrapTransformation.TryTransform(inventory, out _);
                    }
                }
            }
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
