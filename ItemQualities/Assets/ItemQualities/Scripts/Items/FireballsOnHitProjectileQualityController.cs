using HG;
using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using RoR2;
using RoR2.Projectile;
using RoR2BepInExPack.GameAssetPathsBetter;
using UnityEngine;

namespace ItemQualities.Items
{
    public sealed class FireballsOnHitProjectileQualityController : MonoBehaviour
    {
        [SystemInitializer]
        static void Init()
        {
            AddressableUtil.LoadAssetAsync<GameObject>(RoR2_Base_FireballsOnHit.FireMeatBall_prefab).OnSuccess(fireballPrefab =>
            {
                fireballPrefab.EnsureComponent<FireballsOnHitProjectileQualityController>();
            });
        }

        bool Initialized = false;
        void FixedUpdate()
        {
            if (Initialized)
                return;
            Initialized = true;
            if (!TryGetComponent(out ProjectileController projectileController) || !projectileController.ghost)
                return;
            GameObject owner = projectileController.owner;
            CharacterBody ownerBody = owner ? owner.GetComponent<CharacterBody>() : null;

            float scaleIncrease = FireballsOnHit.GetFireballScaleIncrease(ownerBody);

            if (scaleIncrease > 0f)
            {
                transform.localScale *= 1 + (scaleIncrease / 7);
                if (TryGetComponent(out ProjectileExplosion projectileExplosion))
                {
                    projectileExplosion.SetExplosionRadius(projectileExplosion.blastRadius + scaleIncrease);
                }
            }

            Transform trailRenderer = projectileController.ghost.transform.Find("TrailRenderer");
            if (trailRenderer && trailRenderer.TryGetComponent(out TrailRenderer trailRendererComp))
            {
                trailRendererComp.widthMultiplier = 1 + (scaleIncrease / 7);
                trailRendererComp.time = 1 + (scaleIncrease / 7);
            }

            projectileController.ghost.transform.localScale = Vector3.one * (1 + (scaleIncrease / 7));
        }
    }
}
