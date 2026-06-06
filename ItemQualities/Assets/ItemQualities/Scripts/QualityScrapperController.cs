using HG;
using ItemQualities.ContentManagement;
using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using R2API;
using RoR2;
using RoR2.UI;
using RoR2BepInExPack.GameAssetPathsBetter;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace ItemQualities
{
    public sealed class QualityScrapperController : MonoBehaviour
    {
        private static SceneIndex _moon2SceneIndex = SceneIndex.Invalid;

        [SystemInitializer(typeof(SceneCatalog))]
        private static void Init()
        {
            _moon2SceneIndex = SceneCatalog.FindSceneIndex("moon2");
            if (_moon2SceneIndex != SceneIndex.Invalid)
            {
                Stage.onServerStageBegin += onServerStageBegin;
            }
            else
            {
                Log.Warning("Failed to find moon2 scene index");
            }
        }

        private static void onServerStageBegin(Stage stage)
        {
            SceneDef sceneDef = stage ? stage.sceneDef : null;
            SceneIndex sceneIndex = sceneDef ? sceneDef.sceneDefIndex : SceneIndex.Invalid;
            if (sceneIndex == SceneIndex.Invalid)
            {
                return;
            }

            if (sceneIndex == _moon2SceneIndex)
            {
                GameObject qualityScrapperInstance = Instantiate(ItemQualitiesContent.NetworkedPrefabs.QualityScrapper, new Vector3(-208.6049f, -144.4923f, -335.8936f), Quaternion.Euler(352.3429f, 0.425f, 353.6553f));

                NetworkServer.Spawn(qualityScrapperInstance);
            }
        }

        [ContentInitializer]
        private static IEnumerator LoadContent(ContentInitializerArgs args)
        {
            AsyncOperationHandle<InteractableSpawnCard> scrapperSpawnCardLoad = AddressableUtil.LoadTempAssetAsync<InteractableSpawnCard>(RoR2_Base_Scrapper.iscScrapper_asset);
            scrapperSpawnCardLoad.OnSuccess(scrapperSpawnCard =>
            {
                InteractableSpawnCard qualityScrapperSpawnCard = Instantiate(scrapperSpawnCard);
                qualityScrapperSpawnCard.name = "iscQualityScrapper";

                GameObject qualityScrapperPrefab = scrapperSpawnCard.prefab.InstantiateClone(nameof(ItemQualitiesContent.NetworkedPrefabs.QualityScrapper));
                QualityScrapperController qualityScrapperController = qualityScrapperPrefab.AddComponent<QualityScrapperController>();

                Renderer qualityScrapperRenderer = null;
                if (qualityScrapperPrefab.TryGetComponent(out ModelLocator modelLocator) && modelLocator.modelTransform)
                {
                    qualityScrapperRenderer = modelLocator.modelTransform.GetComponentInChildren<SkinnedMeshRenderer>();
                }
                else
                {
                    qualityScrapperRenderer = qualityScrapperPrefab.GetComponentInChildren<SkinnedMeshRenderer>();
                }

                if (qualityScrapperRenderer)
                {
                    if (args.ContentPack.materials.TryGetAsset("mat" + nameof(ItemQualitiesContent.Materials.QualityScrapper), out Material material))
                    {
                        qualityScrapperRenderer.sharedMaterial = material;
                    }
                    else
                    {
                        Log.Error("Failed to find asset matQualityScrapper in content pack");
                    }
                }

                if (qualityScrapperPrefab.TryGetComponent(out PickupPickerController pickupPickerController))
                {
                    int setScrapperOptionsEventIndex = -1;

                    int persistentCallCount = pickupPickerController.onServerInteractionBegin.GetPersistentEventCount();
                    for (int i = 0; i < persistentCallCount; i++)
                    {
                        Object target = pickupPickerController.onServerInteractionBegin.GetPersistentTarget(i);
                        string methodName = pickupPickerController.onServerInteractionBegin.GetPersistentMethodName(i);

                        if (target is PickupPickerController && methodName == nameof(PickupPickerController.SetOptionsFromInteractor))
                        {
                            setScrapperOptionsEventIndex = i;
                            break;
                        }
                    }

                    if (setScrapperOptionsEventIndex != -1)
                    {
                        pickupPickerController.onServerInteractionBegin.SetPersistentListenerState(setScrapperOptionsEventIndex, UnityEventCallState.Off);
                    }
                    else
                    {
                        Log.Warning("Failed to find PickupPickerController.SetOptionsFromInteractor call in onServerInteractionBegin");
                    }

                    pickupPickerController.onServerInteractionBegin.AddPersistentListener(qualityScrapperController.SetOptionsFromInteractor);

                    pickupPickerController.contextString = "QUALITY_SCRAPPER_CONTEXT";

                    GameObject qualityScrapperPickerPanel = pickupPickerController.panelPrefab.InstantiateClone("QualityScrapperPickerPanel", false);
                    Transform titleLabelTransform = qualityScrapperPickerPanel.transform.Find("MainPanel/Juice/Label");
                    if (titleLabelTransform && titleLabelTransform.TryGetComponent(out LanguageTextMeshController titleLabel))
                    {
                        titleLabel._token = "QUALITY_SCRAPPER_POPUP_TEXT";
                    }

                    pickupPickerController.panelPrefab = qualityScrapperPickerPanel;
                    args.ContentPack.prefabs.Add(qualityScrapperPickerPanel);
                }

                if (qualityScrapperPrefab.TryGetComponent(out GenericDisplayNameProvider genericDisplayNameProvider))
                {
                    genericDisplayNameProvider.displayToken = "QUALITY_SCRAPPER_NAME";
                }

                if (qualityScrapperPrefab.TryGetComponent(out GenericInspectInfoProvider genericInspectInfoProvider))
                {
                    genericInspectInfoProvider.InspectInfo = Instantiate(genericInspectInfoProvider.InspectInfo);
                    genericInspectInfoProvider.InspectInfo.name = "idQualityScrapper";
                    genericInspectInfoProvider.InspectInfo.Info.TitleToken = "QUALITY_SCRAPPER_NAME";
                    genericInspectInfoProvider.InspectInfo.Info.DescriptionToken = "QUALITY_SCRAPPER_DESCRIPTION";
                }

                qualityScrapperSpawnCard.prefab = qualityScrapperPrefab;

                args.ContentPack.networkedObjectPrefabs.Add(qualityScrapperPrefab);
                args.ContentPack.spawnCards.Add(qualityScrapperSpawnCard);
            });

            return scrapperSpawnCardLoad.AsProgressCoroutine(args.ProgressReceiver);
        }

        private PickupPickerController _pickupPickerController;

        private void Awake()
        {
            _pickupPickerController = GetComponent<PickupPickerController>();
        }

        public void SetOptionsFromInteractor(Interactor interactor)
        {
            using var _ = ListPool<PickupPickerController.Option>.RentCollection(out List<PickupPickerController.Option> optionsList);
            GetGeneratedOptionsFromInteractor(interactor, optionsList);
            _pickupPickerController.SetOptionsServer(optionsList.ToArray());
        }

        private void GetGeneratedOptionsFromInteractor(Interactor interactor, List<PickupPickerController.Option> options)
        {
            if (interactor && interactor.TryGetComponent(out CharacterBody interactorBody) && interactorBody.inventory)
            {
                ListUtils.EnsureCapacity(options, interactorBody.inventory.itemAcquisitionOrder.Count);

                foreach (ItemIndex itemIndex in interactorBody.inventory.itemAcquisitionOrder)
                {
                    ItemDef itemDef = ItemCatalog.GetItemDef(itemIndex);
                    if (QualityCatalog.GetQualityTier(itemIndex) == QualityTier.None)
                        continue;

                    if (!itemDef.canRemove || itemDef.hidden || itemDef.tier == ItemTier.NoTier)
                        continue;

                    if (itemDef.DoesNotContainTag(ItemTag.Scrap))
                        continue;

                    if (interactorBody.inventory.GetItemCountPermanent(itemIndex) > 0)
                    {
                        options.Add(new PickupPickerController.Option
                        {
                            available = true,
                            pickup = new UniquePickup(PickupCatalog.FindPickupIndex(itemIndex)),
                        });
                    }
                }
            }
        }
    }
}
