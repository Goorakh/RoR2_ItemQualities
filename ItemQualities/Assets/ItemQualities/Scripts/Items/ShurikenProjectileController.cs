using ItemQualities.Utilities.Extensions;
using RoR2;
using RoR2.Projectile;
using UnityEngine;

namespace ItemQualities.Items
{
    public class ShurikenProjectileController : MonoBehaviour
    {
        void Awake()
        {
            ProjectileController projectileController = GetComponent<ProjectileController>();
            if (!projectileController)
            {
                Log.Error($"{Util.GetGameObjectHierarchyName(gameObject)} is missing ProjectileController component");
                enabled = false;
                return;
            }

            projectileController.onInitialized += onInitialized;
        }

        void onInitialized(ProjectileController projectileController)
        {
            ItemQualityCounts primarySkillShuriken = default;
            if (projectileController.owner && projectileController.owner.TryGetComponent(out CharacterBody ownerBody) && ownerBody.inventory)
            {
                primarySkillShuriken = ownerBody.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.PrimarySkillShuriken);
            }

            Vector3 scale = transform.localScale;

            if (primarySkillShuriken.TotalQualityCount > 0)
            {
                float scaleMultAdd = (0.1f * primarySkillShuriken.UncommonCount) +
                                     (0.3f * primarySkillShuriken.RareCount) +
                                     (0.5f * primarySkillShuriken.EpicCount) +
                                     (0.8f * primarySkillShuriken.LegendaryCount);

                if (scaleMultAdd > 0f)
                {
                    scale *= 1f + scaleMultAdd;
                }
            }

            transform.localScale = scale;
        }
    }
}
