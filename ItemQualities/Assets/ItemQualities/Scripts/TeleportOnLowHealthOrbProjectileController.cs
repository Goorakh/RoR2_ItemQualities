using ItemQualities.Utilities.Extensions;
using RoR2;
using RoR2.Projectile;
using UnityEngine;

namespace ItemQualities
{
    [RequireComponent(typeof(ProjectileController))]
    public sealed class TeleportOnLowHealthOrbProjectileController : MonoBehaviour
    {
        private ProjectileController _projectileController;
        private ProjectileDirectionalTargetFinder _projectileDirectionalTargetFinder;

        private void Awake()
        {
            _projectileController = GetComponent<ProjectileController>();
            _projectileDirectionalTargetFinder = GetComponent<ProjectileDirectionalTargetFinder>();
        }

        private void Start()
        {
            ItemQualityCounts teleportOnLowHealth = default;
            if (_projectileController.owner && _projectileController.owner.TryGetComponent(out CharacterBody ownerBody) && ownerBody.inventory)
            {
                teleportOnLowHealth = ownerBody.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.TeleportOnLowHealth);
            }

            if (teleportOnLowHealth.TotalQualityCount == 0)
            {
                teleportOnLowHealth.UncommonCount = 1;
            }

            float radius = (3f * teleportOnLowHealth.UncommonCount) +
                           (6f * teleportOnLowHealth.RareCount) +
                           (10f * teleportOnLowHealth.EpicCount) +
                           (15f * teleportOnLowHealth.LegendaryCount);

            transform.localScale = new Vector3(radius, radius, radius);

            if (_projectileDirectionalTargetFinder)
            {
                _projectileDirectionalTargetFinder.lookRange += radius;
            }
        }
    }
}
