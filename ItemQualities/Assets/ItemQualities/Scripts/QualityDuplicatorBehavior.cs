using RoR2;
using RoR2.Audio;
using RoR2.ContentManagement;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Events;
using UnityEngine.Networking;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace ItemQualities
{
    [RequireComponent(typeof(PickupPickerController))]
    [RequireComponent(typeof(ShopTerminalBehavior))]
    public sealed class QualityDuplicatorBehavior : NetworkBehaviour, IInteractable, IHologramContentProvider
    {
        static EffectIndex _itemTakenOrbEffectIndex = EffectIndex.Invalid;
        static EffectIndex _regeneratingScrapExplosionDisplayEffectIndex = EffectIndex.Invalid;

        [SystemInitializer(typeof(EffectCatalogUtils))]
        static void Init()
        {
            _itemTakenOrbEffectIndex = EffectCatalogUtils.FindEffectIndex("ItemTakenOrbEffect");
            if (_itemTakenOrbEffectIndex == EffectIndex.Invalid)
            {
                Log.Error("Failed to find item taken orb effect index");
            }

            _regeneratingScrapExplosionDisplayEffectIndex = EffectCatalogUtils.FindEffectIndex("RegeneratingScrapExplosionDisplay");
            if (_regeneratingScrapExplosionDisplayEffectIndex == EffectIndex.Invalid)
            {
                Log.Error("Failed to find regenerating scrap item display effect index");
            }
        }

        public CostTypeIndex CostTypeIndex = CostTypeIndex.WhiteItem;

        public int Cost = 1;

        public string ContextToken;

        public AssetReferenceGameObject HologramContentPrefab = new AssetReferenceGameObject(RoR2BepInExPack.GameAssetPathsBetter.RoR2_Base_Common_VFX.CostHologramContent_prefab);

        public UnityEvent<Interactor> OnPurchase;

        [SyncVar]
        bool _available = true;

        Xoroshiro128Plus _rng;

        AsyncOperationHandle<GameObject> _hologramContentPrefabLoad;

        PickupPickerController _pickerController;

        NetworkUIPromptController _promptController;

        ShopTerminalBehavior _terminalBehavior;

        readonly List<PickupIndex> _selectedPickups = new List<PickupIndex>();

        public static event Action<QualityDuplicatorBehavior, Interactor, IReadOnlyList<PickupIndex>> OnPickupsSelectedForPurchase;

        void Awake()
        {
            _terminalBehavior = GetComponent<ShopTerminalBehavior>();
            _pickerController = GetComponent<PickupPickerController>();
            _promptController = GetComponent<NetworkUIPromptController>();
            _promptController.onDisplayEnd += onPromptDisplayEnd;

            _hologramContentPrefabLoad = AssetAsyncReferenceManager<GameObject>.LoadAsset(HologramContentPrefab);
        }

        void Start()
        {
            if (NetworkServer.active)
            {
                _rng = new Xoroshiro128Plus(Run.instance.treasureRng.nextUlong);
            }
        }

        void OnDestroy()
        {
            AssetAsyncReferenceManager<GameObject>.UnloadAsset(HologramContentPrefab);
        }

        void onPromptDisplayEnd(NetworkUIPromptController promptController, LocalUser localUser, CameraRigController cameraRig)
        {
            _selectedPickups.Clear();
            _pickerController.enabled = false;
        }

        public void OnPickupSelected(int pickupIndexInt)
        {
            /*
            PickupIndex pickupIndex = new PickupIndex(pickupIndexInt);
            PickupDef pickupDef = PickupCatalog.GetPickupDef(pickupIndex);

            CharacterMaster interactorMaster = _promptController.currentParticipantMaster;
            CharacterBody interactorBody = interactorMaster ? interactorMaster.GetBody() : null;
            Interactor interactor = interactorBody ? interactorBody.GetComponent<Interactor>() : null;
            Inventory interactorInventory = interactorMaster ? interactorMaster.inventory : null;

            int pickupCount = 1;
            if (pickupDef != null)
            {
                if (pickupDef.itemIndex != ItemIndex.None)
                {
                    if (interactorInventory)
                    {
                        pickupCount = interactorInventory.GetItemCount(pickupDef.itemIndex);
                    }
                }
            }

            int numPickupsToTake = Math.Min(Cost - _selectedPickups.Count, pickupCount);
            for (int i = 0; i < numPickupsToTake; i++)
            {
                _selectedPickups.Add(pickupIndex);
            }

            if (_selectedPickups.Count >= Cost)
            {
                if (interactorInventory)
                {
                    bool tookRegeneratingScrap = false;
                    foreach (PickupIndex selectedPickupIndex in _selectedPickups)
                    {
                        PickupDef selectedPickupDef = PickupCatalog.GetPickupDef(selectedPickupIndex);
                        if (selectedPickupDef != null && selectedPickupDef.itemIndex != ItemIndex.None)
                        {
                            if (interactorBody)
                            {
                                createItemTakenOrb(interactorBody.corePosition, gameObject, selectedPickupDef.itemIndex);

                                ItemIndex baseItemIndex = QualityCatalog.GetItemIndexOfQuality(selectedPickupDef.itemIndex, QualityTier.None);
                                if (baseItemIndex == DLC1Content.Items.RegeneratingScrap.itemIndex)
                                {
                                    interactorInventory.GiveItem(DLC1Content.Items.RegeneratingScrapConsumed);
                                    tookRegeneratingScrap = true;

                                    EntitySoundManager.EmitSoundServer(NetworkSoundEventCatalog.FindNetworkSoundEventIndex("Play_item_proc_regenScrap_consume"), interactorBody.gameObject);
                                    if (_regeneratingScrapExplosionDisplayEffectIndex != EffectIndex.Invalid)
                                    {
                                        if (interactorBody.modelLocator &&
                                            interactorBody.modelLocator.modelTransform &&
                                            interactorBody.modelLocator.TryGetComponent(out CharacterModel interactorCharacterModel))
                                        {
                                            List<GameObject> regeneratingScrapItemDisplayObjects = interactorCharacterModel.GetItemDisplayObjects(DLC1Content.Items.RegeneratingScrap.itemIndex);
                                            if (regeneratingScrapItemDisplayObjects.Count > 0)
                                            {
                                                GameObject displayObject = regeneratingScrapItemDisplayObjects[0];

                                                EffectManager.SpawnEffect(_regeneratingScrapExplosionDisplayEffectIndex, new EffectData
                                                {
                                                    origin = displayObject.transform.position,
                                                    rotation = displayObject.transform.rotation
                                                }, true);
                                            }
                                        }
                                    }

                                    EffectManager.SimpleMuzzleFlash(Addressables.LoadAssetAsync<GameObject>("RoR2/DLC1/RegeneratingScrap/RegeneratingScrapExplosionInPrinter.prefab").WaitForCompletion(), gameObject, "DropPivot", true);
                                }
                            }

                            interactorInventory.RemoveItem(selectedPickupDef.itemIndex);
                        }
                    }

                    if (tookRegeneratingScrap && interactorMaster)
                    {
                        CharacterMasterNotificationQueue.SendTransformNotification(interactorMaster, DLC1Content.Items.RegeneratingScrap.itemIndex, DLC1Content.Items.RegeneratingScrapConsumed.itemIndex, CharacterMasterNotificationQueue.TransformationType.Default);
                    }
                }

                OnPickupsSelectedForPurchase?.Invoke(this, interactor, _selectedPickups.AsReadOnly());
                OnPurchase?.Invoke(interactor);
            }
            */
        }

        [Server]
        static void createItemTakenOrb(Vector3 effectOrigin, GameObject targetObject, ItemIndex itemIndex)
        {
            if (_itemTakenOrbEffectIndex != EffectIndex.Invalid)
            {
                EffectData effectData = new EffectData
                {
                    origin = effectOrigin,
                    genericFloat = 1.5f,
                    genericUInt = (uint)(itemIndex + 1)
                };
                effectData.SetNetworkedObjectReference(targetObject);

                EffectManager.SpawnEffect(_itemTakenOrbEffectIndex, effectData, true);
            }
        }

        [Server]
        public void SetAvailable(bool available)
        {
            _available = available;
        }

        public void SetQualityScrapOptionsFromInteractor(Interactor interactor)
        {
            /*
            CharacterBody interactorBody = interactor ? interactor.GetComponent<CharacterBody>() : null;
            Inventory interactorInventory = interactorBody ? interactorBody.inventory : null;
            if (!interactorInventory)
                return;

            ItemTier costTier = ItemTier.NoTier;
            CostTypeDef costTypeDef = CostTypeCatalog.GetCostTypeDef(CostTypeIndex);
            if (costTypeDef != null)
            {
                costTier = costTypeDef.itemTier;
            }

            List<PickupPickerController.Option> scrapOptions = new List<PickupPickerController.Option>();
            List<PickupPickerController.Option> priorityScrapOptions = new List<PickupPickerController.Option>();

            foreach (ItemIndex itemIndex in interactorInventory.itemAcquisitionOrder)
            {
                ItemDef itemDef = ItemCatalog.GetItemDef(itemIndex);
                if (itemDef && itemDef.tier == costTier && QualityCatalog.GetQualityTier(itemIndex) > QualityTier.None)
                {
                    PickupPickerController.Option option = new PickupPickerController.Option
                    {
                        pickupIndex = PickupCatalog.FindPickupIndex(itemIndex),
                        available = true,
                    };

                    if (itemDef.ContainsTag(ItemTag.PriorityScrap))
                    {
                        priorityScrapOptions.Add(option);
                    }
                    else if (itemDef.ContainsTag(ItemTag.Scrap))
                    {
                        scrapOptions.Add(option);
                    }
                }
            }

            List<PickupPickerController.Option> finalOptions = priorityScrapOptions.Count > 0 ? priorityScrapOptions : scrapOptions;

            _pickerController.SetOptionsServer(finalOptions.ToArray());
            */
        }

        bool canBeAffordedByInteractor(Interactor interactor)
        {
            CostTypeDef costTypeDef = CostTypeCatalog.GetCostTypeDef(CostTypeIndex);
            return costTypeDef.IsAffordable(Cost, interactor);
        }
        
        bool hasAmbiguousPayment(Interactor interactor)
        {
            CharacterBody activatorBody = interactor ? interactor.GetComponent<CharacterBody>() : null;
            Inventory activatorInventory = activatorBody ? activatorBody.inventory : null;

            if (activatorInventory)
            {
                ItemTier costTier = ItemTier.NoTier;
                CostTypeDef costTypeDef = CostTypeCatalog.GetCostTypeDef(CostTypeIndex);
                if (costTypeDef != null)
                {
                    costTier = costTypeDef.itemTier;
                }

                bool ambiguousScrapCost = false;
                ItemIndex encounteredScrapItemIndex = ItemIndex.None;

                bool ambiguousPriorityScrapCost = false;
                ItemIndex encounteredPriorityScrapItemIndex = ItemIndex.None;

                foreach (ItemIndex itemIndex in activatorInventory.itemAcquisitionOrder)
                {
                    ItemDef itemDef = ItemCatalog.GetItemDef(itemIndex);
                    if (itemDef && itemDef.canRemove && itemDef.tier == costTier && QualityCatalog.GetQualityTier(itemIndex) > QualityTier.None)
                    {
                        if (itemDef.ContainsTag(ItemTag.PriorityScrap))
                        {
                            if (encounteredPriorityScrapItemIndex != ItemIndex.None)
                            {
                                if (encounteredPriorityScrapItemIndex != itemIndex)
                                {
                                    ambiguousPriorityScrapCost = true;
                                }
                            }
                            else
                            {
                                encounteredPriorityScrapItemIndex = itemIndex;
                            }
                        }
                        else if (itemDef.ContainsTag(ItemTag.Scrap))
                        {
                            if (encounteredScrapItemIndex != ItemIndex.None)
                            {
                                if (encounteredScrapItemIndex != itemIndex)
                                {
                                    ambiguousScrapCost = true;
                                }
                            }
                            else
                            {
                                encounteredScrapItemIndex = itemIndex;
                            }
                        }
                    }
                }

                if (encounteredPriorityScrapItemIndex != ItemIndex.None)
                    return ambiguousPriorityScrapCost;

                if (encounteredScrapItemIndex != ItemIndex.None)
                    return ambiguousScrapCost;
            }

            return false;
        }

        public string GetContextString(Interactor activator)
        {
            return Language.GetString(ContextToken);
        }

        public Interactability GetInteractability(Interactor activator)
        {
            if (!_available)
                return Interactability.Disabled;

            if (!_promptController || _promptController.inUse)
                return Interactability.ConditionsNotMet;

            if (!canBeAffordedByInteractor(activator))
                return Interactability.ConditionsNotMet;

            return Interactability.Available;
        }

        public void OnInteractionBegin(Interactor activator)
        {
            /*
            if (hasAmbiguousPayment(activator))
            {
                _pickerController.enabled = true;
                _pickerController.OnInteractionBegin(activator);
            }
            else
            {
                _pickerController.enabled = false;

                CharacterBody activatorBody = activator.GetComponent<CharacterBody>();

                PickupDef currentPickup = PickupCatalog.GetPickupDef(_terminalBehavior.CurrentPickupIndex());
                ItemIndex currentItem = currentPickup != null ? currentPickup.itemIndex : ItemIndex.None;

                CostTypeDef costTypeDef = CostTypeCatalog.GetCostTypeDef(CostTypeIndex);
                CostTypeDef.PayCostResults payCostResults = costTypeDef.PayCost(Cost, activator, gameObject, _rng, currentItem);

                if (activatorBody)
                {
                    foreach (ItemIndex itemIndex in payCostResults.itemsTaken)
                    {
                        createItemTakenOrb(activatorBody.corePosition, gameObject, itemIndex);
                    }
                }

                OnPurchase?.Invoke(activator);
            }
            */
        }

        public bool ShouldIgnoreSpherecastForInteractibility(Interactor activator)
        {
            return false;
        }

        public bool ShouldProximityHighlight()
        {
            return true;
        }

        public bool ShouldShowOnScanner()
        {
            return _available;
        }

        public bool ShouldDisplayHologram(GameObject viewer)
        {
            return _available;
        }

        public GameObject GetHologramContentPrefab()
        {
            if (_hologramContentPrefabLoad.IsValid())
            {
                _hologramContentPrefabLoad.WaitForCompletion();
                return _hologramContentPrefabLoad.Result;
            }

            return null;
        }

        public void UpdateHologramContent(GameObject hologramContentObject, Transform viewerBody)
        {
            if (hologramContentObject.TryGetComponent(out CostHologramContent hologramContent))
            {
                hologramContent.displayValue = Cost;
                hologramContent.costType = CostTypeIndex;
            }
        }
    }
}
