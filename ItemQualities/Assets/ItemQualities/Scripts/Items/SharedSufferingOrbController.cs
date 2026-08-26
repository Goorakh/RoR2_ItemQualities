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

        private GenericOwnership _ownership;
        private Deployable _deployable;

        private CharacterBody _ownerBody;

        private void Awake()
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

        private void Start()
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

        private void OnDestroy()
        {
            if (_deployable)
            {
                _deployable.onUndeploy?.RemoveListener(onUndeploy);
            }
        }

        private void onUndeploy()
        {
            if (TryGetComponent(out HealthComponent healthComponent))
            {
                healthComponent.Suicide();
            }
        }

        [Server]
        private void recalculateRadius()
        {
            Inventory ownerInventory = _ownerBody ? _ownerBody.inventory : null;

            ItemQualityCounts sharedSuffering = ItemQualityCounts.zero;
            if (ownerInventory)
            {
                sharedSuffering = ownerInventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.SharedSuffering);
            }

            if (sharedSuffering.TotalQualityCount == 0)
                sharedSuffering.UncommonCount = 1;

            float radius = (25f * sharedSuffering.UncommonCount) +
                           (35f * sharedSuffering.RareCount) +
                           (50f * sharedSuffering.EpicCount) +
                           (65f * sharedSuffering.LegendaryCount);

            if (_ownerBody)
            {
                radius = ExplodeOnDeath.GetExplosionRadius(radius, _ownerBody);
            }

            BlastRadius = radius;
        }

        private void refreshIndicator()
        {
            if (RadiusIndicator)
            {
                float diameter = BlastRadius * 2f;
                RadiusIndicator.localScale = new Vector3(diameter, diameter, diameter);
            }
        }

        private void hookSetRadius(float radius)
        {
            BlastRadius = radius;
            refreshIndicator();
        }
    }
}
