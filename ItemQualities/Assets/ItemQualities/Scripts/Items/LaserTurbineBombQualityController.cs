using HG;
using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using RoR2;
using RoR2.Projectile;
using RoR2BepInExPack.GameAssetPathsBetter;
using UnityEngine;

namespace ItemQualities.Items
{
    public class LaserTurbineBombQualityController : MonoBehaviour
    {
        [SystemInitializer]
        static void Init()
        {
            AddressableUtil.LoadTempAssetAsync<GameObject>(RoR2_Base_LaserTurbine.LaserTurbineBomb_prefab).OnSuccess(laserTurbineBomb =>
            {
                laserTurbineBomb.EnsureComponent<LaserTurbineBombQualityController>();
            });
        }

        ProjectileImpactExplosion _projectileImpactExplosion;

        void Awake()
        {
            _projectileImpactExplosion = GetComponent<ProjectileImpactExplosion>();

            if (_projectileImpactExplosion && TryGetComponent(out ProjectileController projectileController))
            {
                projectileController.onInitialized += onInitialized;
            }
        }

        void onInitialized(ProjectileController projectileController)
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
