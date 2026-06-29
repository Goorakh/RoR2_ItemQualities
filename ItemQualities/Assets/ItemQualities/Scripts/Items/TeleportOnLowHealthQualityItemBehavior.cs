using ItemQualities.Utilities.Extensions;
using UnityEngine;

namespace ItemQualities.Items
{
    public sealed class TeleportOnLowHealthQualityItemBehavior : QualityItemBodyBehavior
    {
        [ItemGroupAssociation(QualityItemBehaviorUsageFlags.Server)]
        private static ItemQualityGroup GetItemGroup() => ItemQualitiesContent.ItemQualityGroups.TeleportOnLowHealth;

        private int _maxCharges;
        private float _chargeInterval;

        private float _barrierTimer;

        private void FixedUpdate()
        {
            if (Body.healthComponent.alive && Body.healthComponent.barrier > 0f)
            {
                _barrierTimer += Time.fixedDeltaTime;
                if (_barrierTimer >= _chargeInterval)
                {
                    _barrierTimer -= _chargeInterval;

                    if (Body.GetBuffCounts(ItemQualitiesContent.BuffQualityGroups.TeleportOnLowHealthOrbCharge).TotalQualityCount < _maxCharges)
                    {
                        Body.AddBuff(ItemQualitiesContent.BuffQualityGroups.TeleportOnLowHealthOrbCharge.GetBuffIndex(Stacks.HighestQuality));
                    }
                }
            }
        }

        protected override void OnStacksChanged()
        {
            base.OnStacksChanged();

            ref readonly ItemQualityCounts stacks = ref Stacks;

            QualityTier qualityTier = stacks.HighestQuality;

            int maxCharges;
            switch (qualityTier)
            {
                case QualityTier.None:
                    maxCharges = 0;
                    break;
                case QualityTier.Uncommon:
                    maxCharges = 2;
                    break;
                case QualityTier.Rare:
                    maxCharges = 5;
                    break;
                case QualityTier.Epic:
                    maxCharges = 10;
                    break;
                case QualityTier.Legendary:
                    maxCharges = 20;
                    break;
                default:
                    Log.Warning($"Quality tier {qualityTier} is not implemented");
                    maxCharges = 0;
                    break;
            }

            _maxCharges = maxCharges;

            _chargeInterval = 10f * Mathf.Pow(1f - 0.10f, stacks.UncommonCount) *
                                    Mathf.Pow(1f - 0.30f, stacks.RareCount) *
                                    Mathf.Pow(1f - 0.50f, stacks.EpicCount) *
                                    Mathf.Pow(1f - 0.75f, stacks.LegendaryCount);

            if (qualityTier != QualityTier.None)
            {
                Body.ConvertQualityBuffsToTier(ItemQualitiesContent.BuffQualityGroups.TeleportOnLowHealthOrbCharge, qualityTier);
            }
        }
    }
}
