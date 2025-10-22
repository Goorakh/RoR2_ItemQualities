using RoR2;
using RoR2.Projectile;
using UnityEngine.Networking;

namespace ItemQualities.Items
{
    public class ElementalRingVoidBlackHoleProjectileController : NetworkBehaviour
    {
        [SyncVar]
        float _scaleMultiplier = 1f;

        bool _appliedScaleMultiplier;

        RadialForce _radialForce;

        void Awake()
        {
            if (!TryGetComponent(out _radialForce))
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
            if (projectileController && projectileController.owner && projectileController.owner.TryGetComponent(out CharacterBody ownerBody))
            {
                elementalRingVoid = ItemQualitiesContent.ItemQualityGroups.ElementalRingVoid.GetItemCounts(ownerBody.inventory);
            }

            float scaleMultiplier = 1f;

            if (elementalRingVoid.TotalQualityCount > 0)
            {
                scaleMultiplier += (0.20f * elementalRingVoid.UncommonCount) +
                                   (0.40f * elementalRingVoid.RareCount) +
                                   (0.60f * elementalRingVoid.EpicCount) +
                                   (1.00f * elementalRingVoid.LegendaryCount);
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
