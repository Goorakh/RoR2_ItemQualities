using RoR2;
using RoR2.Projectile;
using UnityEngine;
using UnityEngine.Networking;

namespace ItemQualities.Items
{
    public class FireTornadoProjectileController : NetworkBehaviour
    {
        [SyncVar]
        float _overrideLifetime = -1f;

        ProjectileSimple _projectileSimple;

        void Awake()
        {
            _projectileSimple = GetComponent<ProjectileSimple>();
            if (!_projectileSimple)
            {
                Log.Error($"{Util.GetGameObjectHierarchyName(gameObject)} is missing ProjectileSimple component");
                enabled = false;
                return;
            }

            ProjectileController projectileController = GetComponent<ProjectileController>();
            if (!projectileController)
            {
                Log.Error($"{Util.GetGameObjectHierarchyName(gameObject)} is missing ProjectileController component");
                enabled = false;
                return;
            }

            if (NetworkServer.active)
            {
                projectileController.onInitialized += onInitializedServer;
            }
        }

        [Server]
        void onInitializedServer(ProjectileController projectileController)
        {
            ItemQualityCounts fireRing = default;
            if (projectileController.owner && projectileController.owner.TryGetComponent(out CharacterBody ownerBody))
            {
                fireRing = ItemQualitiesContent.ItemQualityGroups.FireRing.GetItemCountsEffective(ownerBody.inventory);
            }

            float lifetime = _projectileSimple.lifetime;
            Vector3 scale = transform.localScale;

            if (fireRing.TotalQualityCount > 0)
            {
                float lifetimeMultAdd = (0.05f * fireRing.UncommonCount) +
                                        (0.15f * fireRing.RareCount) +
                                        (0.30f * fireRing.EpicCount) +
                                        (0.50f * fireRing.LegendaryCount);

                lifetime *= 1f + lifetimeMultAdd;

                float scaleMultAdd = (0.20f * fireRing.UncommonCount) +
                                     (0.40f * fireRing.RareCount) +
                                     (0.60f * fireRing.EpicCount) +
                                     (1.00f * fireRing.LegendaryCount);

                scale *= 1f + scaleMultAdd;
            }

            _overrideLifetime = lifetime;
            transform.localScale = scale;
            applyLifetimeOverride();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            applyLifetimeOverride();
        }

        void applyLifetimeOverride()
        {
            if (_projectileSimple && _overrideLifetime > 0f)
            {
                _projectileSimple.lifetime = _overrideLifetime;
            }
        }
    }
}
