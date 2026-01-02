using ItemQualities.Utilities.Extensions;
using RoR2;
using RoR2.Projectile;
using UnityEngine.Networking;

namespace ItemQualities.Items
{
    public sealed class ElementalRingVoidBlackHoleProjectileController : NetworkBehaviour
    {
        [SyncVar]
        float _scaleMultiplier = 1f;

        bool _appliedScaleMultiplier;

        RadialForce _radialForce;

        void Awake()
        {
            if (TryGetComponent(out RadialForce radialForce))
            {
                _radialForce = radialForce;
            }
            else
            {
                Log.Error($"{Util.GetGameObjectHierarchyName(gameObject)} is missing RadialForce component");
                enabled = false;
            }

            if (NetworkServer.active)
            {
                if (TryGetComponent(out ProjectileController projectileController))
                {
                    projectileController.onInitialized += onInitializedServer;
                }
                else
                {
                    Log.Error($"{Util.GetGameObjectHierarchyName(gameObject)} is missing ProjectileController component");
                    enabled = false;
                }
            }
        }

        [Server]
        void onInitializedServer(ProjectileController projectileController)
        {
            ItemQualityCounts elementalRingVoid = default;
            if (projectileController && projectileController.owner && projectileController.owner.TryGetComponent(out CharacterBody ownerBody) && ownerBody.inventory)
            {
                elementalRingVoid = ownerBody.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.ElementalRingVoid);
            }

            float scaleMultiplier;
            switch (elementalRingVoid.HighestQuality)
            {
                case QualityTier.None:
                    scaleMultiplier = 1f;
                    break;
                case QualityTier.Uncommon:
                    scaleMultiplier = 1.33f;
                    break;
                case QualityTier.Rare:
                    scaleMultiplier = 1.66f;
                    break;
                case QualityTier.Epic:
                    scaleMultiplier = 2.33f;
                    break;
                case QualityTier.Legendary:
                    scaleMultiplier = 3f;
                    break;
                default:
                    scaleMultiplier = 1f;
                    Log.Error($"Quality tier {elementalRingVoid.HighestQuality} is not implemented");
                    break;
            }

            _scaleMultiplier = scaleMultiplier;

            if (scaleMultiplier > 1f)
            {
                transform.localScale *= scaleMultiplier;
            }

            applyScaleMultiplier();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            applyScaleMultiplier();
        }

        void applyScaleMultiplier()
        {
            if (_appliedScaleMultiplier)
                return;

            _radialForce.radius *= _scaleMultiplier;

            _appliedScaleMultiplier = true;
        }
    }
}
