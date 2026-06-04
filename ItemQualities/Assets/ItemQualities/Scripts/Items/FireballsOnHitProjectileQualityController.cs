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

        void Awake()
        {
            if (!NetworkServer.active)
                return;
            if (TryGetComponent(out ProjectileController projectileController))
            {
                projectileController.onInitialized += onInitializedServer;
            }
        }

        void onInitializedServer(ProjectileController projectileController)
        {
            GameObject owner = projectileController ? projectileController.owner : null;
            CharacterBody ownerBody = owner ? owner.GetComponent<CharacterBody>() : null;

            float scaleIncrease = FireballsOnHit.GetFireballScaleIncrease(ownerBody);

            if (scaleIncrease > 0f)
            {
                if (TryGetComponent(out ProjectileExplosion projectileExplosion))
                {
                    projectileExplosion.SetExplosionRadius(projectileExplosion.blastRadius + scaleIncrease);
                }
            }
        }
    }
}
