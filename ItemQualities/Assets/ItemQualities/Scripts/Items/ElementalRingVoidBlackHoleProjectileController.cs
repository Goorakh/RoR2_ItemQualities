using ItemQualities.Utilities.Extensions;
using RoR2;
using RoR2.Projectile;
using UnityEngine;

namespace ItemQualities.Items
{
    public sealed class ElementalRingVoidBlackHoleProjectileController : MonoBehaviour
    {
        private ProjectileController _projectileController;
        private RadialForce _radialForce;
        private ProjectileExplosion _projectileExplosion;

        private void Awake()
        {
            _projectileController = GetComponent<ProjectileController>();
            _radialForce = GetComponent<RadialForce>();
            _projectileExplosion = GetComponent<ProjectileExplosion>();
        }

        private void Start()
        {
            ItemQualityCounts elementalRingVoid = ItemQualityCounts.zero;
            if (_projectileController.owner && _projectileController.owner.TryGetComponent(out CharacterBody ownerBody) && ownerBody.inventory)
            {
                elementalRingVoid = ownerBody.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.ElementalRingVoid);
            }

            float scaleMultiplier;
            switch (elementalRingVoid.HighestQuality)
            {
                case QualityTier.None:
                    scaleMultiplier = 1f;
                    break;
                case QualityTier.Uncommon:
                    scaleMultiplier = 1.33f;
                    break;
                case QualityTier.Rare:
                    scaleMultiplier = 1.66f;
                    break;
                case QualityTier.Epic:
                    scaleMultiplier = 2.33f;
                    break;
                case QualityTier.Legendary:
                    scaleMultiplier = 3f;
                    break;
                default:
                    scaleMultiplier = 1f;
                    Log.Error($"Quality tier {elementalRingVoid.HighestQuality} is not implemented");
                    break;
            }

            if (scaleMultiplier > 1f)
            {
                transform.localScale *= scaleMultiplier;

                _radialForce.radius *= scaleMultiplier;
                _projectileExplosion.blastRadius *= scaleMultiplier;
            }
        }
    }
}
