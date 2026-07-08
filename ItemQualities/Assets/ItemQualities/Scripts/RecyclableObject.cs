using RoR2;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace ItemQualities
{
    [RequireComponent(typeof(GenericNetworkedObjectAttachment))]
    public sealed class RecyclableObject : NetworkBehaviour, INetworkedObjectAttachmentListener
    {
        private static EffectIndex _recycleEffectIndex = EffectIndex.Invalid;

        private static int[] _recyclableInteractableIndices = Array.Empty<int>();

        [SystemInitializer(typeof(EffectCatalogUtils))]
        private static void Init()
        {
            _recycleEffectIndex = EffectCatalogUtils.FindEffectIndex("OmniRecycleEffect");
            if (_recycleEffectIndex == EffectIndex.Invalid)
            {
                Log.Warning("Failed to find recycle effect index");
            }

            InteractableCatalog.Availability.CallWhenAvailable(() =>
            {
                List<int> recyclableInteractableIndices = new List<int>(InteractableCatalog.InteractableCount);
                for (int i = 0; i < InteractableCatalog.InteractableCount; i++)
                {
                    InteractableDef interactableDef = InteractableCatalog.GetInteractableDef(i);
                    if (interactableDef.Name.Contains("Duplicator", StringComparison.OrdinalIgnoreCase) &&
                        interactableDef.Prefab.TryGetComponent(out ShopTerminalBehavior shopTerminalBehavior) &&
                        interactableDef.Prefab.TryGetComponent(out PurchaseInteraction purchaseInteraction) &&
                        CustomCostTypeIndex.IsQualityItemCostType(purchaseInteraction.costType))
                    {
                        recyclableInteractableIndices.Add(i);
                        Log.Debug($"Including interactable {interactableDef.Name} as recyclable");
                    }
                }

                if (recyclableInteractableIndices.Count > 0)
                {
                    _recyclableInteractableIndices = recyclableInteractableIndices.ToArray();
                    Array.Sort(_recyclableInteractableIndices);

                    InteractableInfoProvider.OnCatalogedInteractableStartGlobal += onCatalogedInteractableStartGlobal;
                }
            });
        }

        private static void onCatalogedInteractableStartGlobal(InteractableInfoProvider interactableInfo)
        {
            if (!NetworkServer.active)
                return;

            if (Array.BinarySearch(_recyclableInteractableIndices, interactableInfo.CatalogIndex) < 0)
                return;

            GameObject recyclableAttachment = Instantiate(ItemQualitiesContent.NetworkedPrefabs.RecyclableObjectAttachment);
            recyclableAttachment.GetComponent<GenericNetworkedObjectAttachment>().AttachToGameObjectAndSpawn(interactableInfo.gameObject);
        }

        public int MaxRecycles = 1;

        [SyncVar]
        private int _numRecycles;

        public Transform IndicatorTransform;

        public bool IsRecyclable
        {
            get
            {
                if (_numRecycles >= MaxRecycles)
                    return false;

                // Don't do anything if were not linked to an interactable yet
                if (!InteractableObject)
                    return false;

                // A tinkered interactable is already recycled
                if (_tinkerAttributes && _tinkerAttributes.tinkers > 0)
                    return false;

                if (_purchaseInteraction && !_purchaseInteraction.available)
                    return false;

                if (_shopTerminalBehavior && _shopTerminalBehavior.pickupIndexIsHidden)
                    return false;

                if (_specialObjectAttributes && !_specialObjectAttributes.isTargetable)
                    return false;

                return true;
            }
        }

        public GameObject InteractableObject => _objectAttachment ? _objectAttachment.AttachedToObject : null;

        private GenericNetworkedObjectAttachment _objectAttachment;

        private TinkerableObjectAttributes _tinkerAttributes;
        private PurchaseInteraction _purchaseInteraction;
        private ShopTerminalBehavior _shopTerminalBehavior;
        private SpecialObjectAttributes _specialObjectAttributes;

        private void Awake()
        {
            _objectAttachment = GetComponent<GenericNetworkedObjectAttachment>();

            // Assign default value to prevent nullrefs before the attached object is discovered
            if (!IndicatorTransform)
            {
                IndicatorTransform = transform;
            }
        }

        private void OnEnable()
        {
            InstanceTracker.Add(this);
        }

        private void OnDisable()
        {
            InstanceTracker.Remove(this);
        }

        [Server]
        public void DoRecycle()
        {
            if (_shopTerminalBehavior)
            {
                _shopTerminalBehavior.RerollPickup();
            }

            _numRecycles++;

            // Count recycle as a tinker so drifter cant do both to a single object
            if (_tinkerAttributes && _tinkerAttributes.tinkers < _tinkerAttributes.maxTinkers)
            {
                _tinkerAttributes.Networktinkers = _tinkerAttributes.tinkers + 1;
            }

            if (_recycleEffectIndex != EffectIndex.Invalid)
            {
                EffectManager.SpawnEffect(_recycleEffectIndex, new EffectData { origin = IndicatorTransform.position }, true);
            }
        }

        void INetworkedObjectAttachmentListener.OnAttachedObjectDiscovered(GenericNetworkedObjectAttachment attachment, GameObject attachedObject)
        {
            _tinkerAttributes = attachedObject ? attachedObject.GetComponent<TinkerableObjectAttributes>() : null;
            _purchaseInteraction = attachedObject ? attachedObject.GetComponent<PurchaseInteraction>() : null;
            _shopTerminalBehavior = attachedObject ? attachedObject.GetComponent<ShopTerminalBehavior>() : null;
            _specialObjectAttributes = attachedObject ? attachedObject.GetComponent<SpecialObjectAttributes>() : null;

            Transform indicatorTransform = transform;
            if (_shopTerminalBehavior && _shopTerminalBehavior.pickupDisplay)
            {
                indicatorTransform = _shopTerminalBehavior.pickupDisplay.transform;
            }
            else if (_tinkerAttributes && _tinkerAttributes.indicatorOffset)
            {
                indicatorTransform = _tinkerAttributes.indicatorOffset;
            }

            IndicatorTransform = indicatorTransform;
        }
    }
}
