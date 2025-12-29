using ItemQualities.Utilities.Extensions;
using RoR2;
using RoR2.Projectile;
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
            if (projectileController.owner && projectileController.owner.TryGetComponent(out CharacterBody ownerBody) && ownerBody.inventory)
            {
                fireRing = ownerBody.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.FireRing);
            }

            float lifetimeMult = 1f;
            float scaleMult = 1f;

            if (fireRing.TotalQualityCount > 0)
            {
                lifetimeMult += (0.05f * fireRing.UncommonCount) +
                                (0.15f * fireRing.RareCount) +
                                (0.30f * fireRing.EpicCount) +
                                (0.50f * fireRing.LegendaryCount);

                scaleMult += (0.50f * fireRing.UncommonCount) +
                             (0.75f * fireRing.RareCount) +
                             (1.00f * fireRing.EpicCount) +
                             (2.00f * fireRing.LegendaryCount);
            }

            if (scaleMult != 1f)
            {
                transform.localScale *= scaleMult;
            }

            if (lifetimeMult != 1f)
            {
                _overrideLifetime = _projectileSimple.lifetime * lifetimeMult;
                applyLifetimeOverride();
            }
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
