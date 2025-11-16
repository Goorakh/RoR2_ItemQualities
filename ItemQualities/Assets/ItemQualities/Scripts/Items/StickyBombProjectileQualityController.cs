using HG;
using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using RoR2;
using RoR2.Projectile;
using RoR2BepInExPack.GameAssetPathsBetter;
using UnityEngine;
using UnityEngine.Networking;

namespace ItemQualities.Items
{
    public sealed class StickyBombProjectileQualityController : MonoBehaviour
    {
        [SystemInitializer]
        static void Init()
        {
            AddressableUtil.LoadAssetAsync<GameObject>(RoR2_Base_StickyBomb.StickyBomb_prefab).OnSuccess(stickyBombPrefab =>
            {
                stickyBombPrefab.EnsureComponent<StickyBombProjectileQualityController>();
            });
        }

        void Awake()
        {
            if (NetworkServer.active)
            {
                if (TryGetComponent(out ProjectileController projectileController))
                {
                    projectileController.onInitialized += onInitializedServer;
                }
            }
        }

        void onInitializedServer(ProjectileController projectileController)
        {
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
