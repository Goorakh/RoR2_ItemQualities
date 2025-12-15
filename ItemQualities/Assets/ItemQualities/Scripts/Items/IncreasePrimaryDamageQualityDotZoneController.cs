using RoR2;
using RoR2.Projectile;
using UnityEngine;
using UnityEngine.Networking;

namespace ItemQualities.Items
{
    [RequireComponent(typeof(ProjectileController))]
    public sealed class IncreasePrimaryDamageQualityDotZoneController : MonoBehaviour
    {
        void Awake()
        {
            if (NetworkServer.active)
            {
                if (TryGetComponent(out ProjectileController projectileController))
                {
                    projectileController.onInitialized += onInitializedServer;
                }
                else
                {
                    Log.Error($"Missing ProjectileController component on {Util.GetGameObjectHierarchyName(gameObject)}");
                    enabled = false;
                    return;
                }
            }
        }

        void onInitializedServer(ProjectileController projectileController)
        {
            GameObject owner = projectileController ? projectileController.owner : null;
            CharacterBody ownerBody = owner ? owner.GetComponent<CharacterBody>() : null;
            Inventory ownerInventory = ownerBody ? ownerBody.inventory : null;

            ItemQualityCounts increasePrimaryDamage = ItemQualitiesContent.ItemQualityGroups.IncreasePrimaryDamage.GetItemCountsEffective(ownerInventory);
            if (increasePrimaryDamage.TotalQualityCount == 0)
                increasePrimaryDamage.UncommonCount = 1;

            float radius;
            switch (increasePrimaryDamage.HighestQuality)
            {
                case QualityTier.Uncommon:
                    radius = 7f;
                    break;
                case QualityTier.Rare:
                    radius = 12f;
                    break;
                case QualityTier.Epic:
                    radius = 20f;
                    break;
                case QualityTier.Legendary:
                    radius = 30f;
                    break;
                default:
                    radius = 7f;
                    Log.Error($"Quality tier {increasePrimaryDamage.HighestQuality} is not implemented");
                    break;
            }

            float diameter = radius * 2f;
            transform.localScale = new Vector3(diameter, 1f, diameter);
        }
    }
}
