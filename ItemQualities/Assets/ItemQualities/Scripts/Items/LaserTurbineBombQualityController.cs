using HG;
using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using RoR2;
using RoR2.Projectile;
using RoR2BepInExPack.GameAssetPathsBetter;
using UnityEngine;

namespace ItemQualities.Items
{
    public sealed class LaserTurbineBombQualityController : MonoBehaviour
    {
        [SystemInitializer]
        private static void Init()
        {
            AddressableUtil.LoadAssetAsync<GameObject>(RoR2_Base_LaserTurbine.LaserTurbineBomb_prefab).OnSuccess(laserTurbineBomb =>
            {
                laserTurbineBomb.EnsureComponent<LaserTurbineBombQualityController>();
            });
        }

        private ProjectileImpactExplosion _projectileImpactExplosion;

        private void Awake()
        {
            _projectileImpactExplosion = GetComponent<ProjectileImpactExplosion>();

            if (_projectileImpactExplosion && TryGetComponent(out ProjectileController projectileController))
            {
                projectileController.onInitialized += onInitialized;
            }
        }

        private void onInitialized(ProjectileController projectileController)
        {
            GameObject owner = projectileController ? projectileController.owner : null;
            CharacterBody ownerBody = owner ? owner.GetComponent<CharacterBody>() : null;

            if (ownerBody)
            {
                _projectileImpactExplosion.blastRadius = LaserTurbine.GetExplosionRadius(_projectileImpactExplosion.blastRadius, ownerBody);
            }
        }
    }
}
