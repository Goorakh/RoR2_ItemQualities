using RoR2;
using RoR2.Projectile;
using UnityEngine;

namespace ItemQualities
{
    [RequireComponent(typeof(ProjectileController))]
    public sealed class ScaleProjectileGhostDurationsToLifetime : MonoBehaviour
    {
        private ProjectileController _projectileController;

        private void Awake()
        {
            _projectileController = GetComponent<ProjectileController>();
        }

        private void Start()
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
