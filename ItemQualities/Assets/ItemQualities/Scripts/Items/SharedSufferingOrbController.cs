using ItemQualities.Utilities.Extensions;
using RoR2;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;

namespace ItemQualities.Items
{
    public sealed class SharedSufferingOrbController : NetworkBehaviour
    {
        public Transform RadiusIndicator;

        [SyncVar(hook = nameof(hookSetRadius))]
        public float BlastRadius = 5f;

        GenericOwnership _ownership;
        Deployable _deployable;

        CharacterBody _ownerBody;

        void Awake()
        {
            if (NetworkServer.active)
            {
                _deployable = GetComponent<Deployable>();
                if (_deployable)
                {
                    _deployable.onUndeploy ??= new UnityEvent();
                    _deployable.onUndeploy.AddListener(onUndeploy);
                }
                else
                {
                    Log.Warning($"Missing Deployable component on {Util.GetGameObjectHierarchyName(gameObject)}");
                }

                _ownership = GetComponent<GenericOwnership>();
                if (!_ownership)
                {
                    Log.Warning($"Missing GenericOwnership component on {Util.GetGameObjectHierarchyName(gameObject)}");
                }
            }
        }

        void Start()
        {
            CharacterBody ownerBody = null;
            if (_ownership)
            {
                GameObject ownerObject = _ownership.ownerObject;
                ownerBody = ownerObject ? ownerObject.GetComponent<CharacterBody>() : null;
            }

            _ownerBody = ownerBody;

            if (NetworkServer.active)
            {
                recalculateRadius();
            }
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            refreshIndicator();
        }

        void OnDestroy()
        {
            if (_deployable)
            {
                _deployable.onUndeploy?.RemoveListener(onUndeploy);
            }
        }

        void onUndeploy()
        {
            if (TryGetComponent(out HealthComponent healthComponent))
            {
                healthComponent.Suicide();
            }
        }

        [Server]
        void recalculateRadius()
        {
            Inventory ownerInventory = _ownerBody ? _ownerBody.inventory : null;
            if (!ownerInventory)
                return;

            ItemQualityCounts sharedSuffering = ownerInventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.SharedSuffering);
            if (sharedSuffering.TotalQualityCount == 0)
                sharedSuffering.UncommonCount = 1;

            float radius = (15f * sharedSuffering.UncommonCount) +
                           (20f * sharedSuffering.RareCount) +
                           (25f * sharedSuffering.EpicCount) +
                           (35f * sharedSuffering.LegendaryCount);

            if (_ownerBody)
            {
                radius = ExplodeOnDeath.GetExplosionRadius(radius, _ownerBody);
            }

            BlastRadius = radius;
        }

        void refreshIndicator()
        {
            if (RadiusIndicator)
            {
                float diameter = BlastRadius * 2f;
                RadiusIndicator.localScale = new Vector3(diameter, diameter, diameter);
            }
        }

        void hookSetRadius(float radius)
        {
            BlastRadius = radius;
            refreshIndicator();
        }
    }
}
