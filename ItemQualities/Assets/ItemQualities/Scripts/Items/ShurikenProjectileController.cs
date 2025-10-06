using RoR2;
using RoR2.Projectile;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

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
            if (projectileController.owner && projectileController.owner.TryGetComponent(out CharacterBody ownerBody))
            {
                primarySkillShuriken = ItemQualitiesContent.ItemQualityGroups.PrimarySkillShuriken.GetItemCounts(ownerBody.inventory);
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
