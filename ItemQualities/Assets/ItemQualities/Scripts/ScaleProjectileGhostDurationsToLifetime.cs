using RoR2;
using RoR2.Projectile;
using UnityEngine;

namespace ItemQualities
{
    [RequireComponent(typeof(ProjectileController))]
    public class ScaleProjectileGhostDurationsToLifetime : MonoBehaviour
    {
        ProjectileController _projectileController;

        void Awake()
        {
            _projectileController = GetComponent<ProjectileController>();
        }

        void Start()
        {
            if (_projectileController.ghost)
            {
                float lifetime = float.PositiveInfinity;
                if (TryGetComponent(out ProjectileSimple projectileSimple))
                {
                    lifetime = Mathf.Min(lifetime, projectileSimple.lifetime);
                }

                if (float.IsFinite(lifetime))
                {
                    if (_projectileController.ghost.TryGetComponent(out ScaleParticleSystemDuration scaleParticleSystemDuration))
                    {
                        scaleParticleSystemDuration.newDuration = lifetime;
                    }
                }
            }
        }
    }
}
