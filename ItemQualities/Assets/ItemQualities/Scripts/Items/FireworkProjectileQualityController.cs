using RoR2;
using RoR2.Projectile;
using UnityEngine;

namespace ItemQualities.Items
{
    [RequireComponent(typeof(ProjectileController))]
    public sealed class FireworkProjectileQualityController : MonoBehaviour
    {
        private void Start()
        {
            if (!TryGetComponent(out ProjectileController projectileController))
                return;

            GameObject owner = projectileController ? projectileController.owner : null;
            CharacterBody ownerBody = owner ? owner.GetComponent<CharacterBody>() : null;

            float scaleMultiplier = Firework.GetFireworkScaleMultiplier(ownerBody);

            if (scaleMultiplier > 1f)
            {
                transform.localScale = Vector3.one * scaleMultiplier;

                // Why does it use a BoxCollider lol
                if (TryGetComponent(out BoxCollider boxCollider))
                {
                    // Undo scale to collider
                    boxCollider.size /= scaleMultiplier;
                }

                if (TryGetComponent(out ProjectileExplosion projectileExplosion))
                {
                    projectileExplosion.blastRadius += scaleMultiplier - 1;
                }
            }
        }
    }
}
