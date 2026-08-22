using HG;
using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using RoR2;
using RoR2.Projectile;
using RoR2BepInExPack.GameAssetPathsBetter;
using UnityEngine;

namespace ItemQualities.Items
{
    public sealed class StickyBombProjectileQualityController : MonoBehaviour
    {
        [SystemInitializer]
        private static void Init()
        {
            AddressableUtil.LoadAssetAsync<GameObject>(RoR2_Base_StickyBomb.StickyBomb_prefab).OnSuccess(stickyBombPrefab =>
            {
                stickyBombPrefab.EnsureComponent<StickyBombProjectileQualityController>();
            });
        }

        private void Start()
        {
            if (!TryGetComponent(out ProjectileController projectileController))
                return;
            GameObject owner = projectileController ? projectileController.owner : null;
            CharacterBody ownerBody = owner ? owner.GetComponent<CharacterBody>() : null;

            float scaleMultiplier = StickyBomb.GetStickyBombScaleMultiplier(ownerBody);

            if (scaleMultiplier >= 1f)
            {
                transform.localScale = transform.localScale * scaleMultiplier;

                if (TryGetComponent(out ProjectileExplosion projectileExplosion))
                {
                    projectileExplosion.SetExplosionRadius(projectileExplosion.blastRadius * scaleMultiplier);
                }
            }
        }
    }
}
