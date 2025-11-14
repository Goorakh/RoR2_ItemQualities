using HG;
using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using RoR2;
using RoR2.Projectile;
using RoR2BepInExPack.GameAssetPathsBetter;
using UnityEngine;

namespace ItemQualities.Items
{
    public class StickyBombQualityFuseController : MonoBehaviour
    {
        [SystemInitializer]
        static void Init()
        {
            AddressableUtil.LoadAssetAsync<GameObject>(RoR2_Base_StickyBomb.StickyBomb_prefab).OnSuccess(stickyBombPrefab =>
            {
                stickyBombPrefab.EnsureComponent<StickyBombQualityFuseController>();
            });
        }

        void Awake()
        {
            if (TryGetComponent(out ProjectileController projectileController))
            {
                projectileController.onInitialized += onInitialized;
            }
        }

        void onInitialized(ProjectileController projectileController)
        {
            if (TryGetComponent(out ProjectileImpactExplosion projectileImpactExplosion))
            {
                GameObject owner = projectileController ? projectileController.owner : null;
                CharacterBody ownerBody = owner ? owner.GetComponent<CharacterBody>() : null;
                if (ownerBody)
                {
                    projectileImpactExplosion.lifetime = StickyBomb.ModifyStickyBombFuse(projectileImpactExplosion.lifetime, ownerBody);
                }
            }
        }
    }
}
