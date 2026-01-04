using RoR2;
using UnityEngine;

namespace ItemQualities.Items
{
    public sealed class SprintArmorQualityItemBehavior : QualityItemBodyBehavior
    {
        [ItemGroupAssociation(QualityItemBehaviorUsageFlags.Server)]
        static ItemQualityGroup GetItemGroup()
        {
            return ItemQualitiesContent.ItemQualityGroups.SprintArmor;
        }

        public static readonly float MaxSprintDeviationDistance = 0.5f;

        float _requiredSprintDuration = 1f;

        bool _wasSprinting = false;

        float _sprintingStopwatch = 0f;
        Plane _sprintPlane = default;

        void OnDisable()
        {
            updateSprinting(false);
        }

        void FixedUpdate()
        {
            updateSprinting(Body.isSprinting);

            if (Body.isSprinting)
            {
                _sprintingStopwatch += Time.fixedDeltaTime;

                if (Mathf.Abs(_sprintPlane.GetDistanceToPoint(Body.footPosition)) >= MaxSprintDeviationDistance)
                {
                    restartSprintTracking();
                }

                if (_sprintingStopwatch >= _requiredSprintDuration)
                {
                    Body.AddTimedBuff(ItemQualitiesContent.Buffs.SprintArmorStrong, 0.5f);
                }
            }
        }

        void updateSprinting(bool isSprinting)
        {
            if (_wasSprinting == isSprinting)
                return;

            _wasSprinting = isSprinting;

            if (isSprinting)
            {
                restartSprintTracking();
            }
        }

        void restartSprintTracking()
        {
            _sprintingStopwatch = 0f;

            Vector3 startPosition = Body.footPosition;

            Vector3 sprintDirection = Body.transform.forward;

            CharacterDirection characterDirection = Body.characterDirection;
            if (characterDirection)
            {
                if (characterDirection.moveVector.sqrMagnitude > Mathf.Epsilon)
                {
                    sprintDirection = characterDirection.moveVector.normalized;
                }
                else
                {
                    sprintDirection = characterDirection.forward;
                }
            }

            _sprintPlane = new Plane(Vector3.Cross(sprintDirection, Vector3.up), startPosition);
        }

        protected override void OnStacksChanged()
        {
            base.OnStacksChanged();

            float requiredSprintDuration;
            switch (Stacks.HighestQuality)
            {
                case QualityTier.None:
                case QualityTier.Uncommon:
                    requiredSprintDuration = 1.0f;
                    break;
                case QualityTier.Rare:
                    requiredSprintDuration = 0.9f;
                    break;
                case QualityTier.Epic:
                    requiredSprintDuration = 0.8f;
                    break;
                case QualityTier.Legendary:
                    requiredSprintDuration = 0.7f;
                    break;
                default:
                    Log.Error($"Quality tier {Stacks.HighestQuality} is not implemented");
                    requiredSprintDuration = 1f;
                    break;
            }

            _requiredSprintDuration = requiredSprintDuration;
        }
    }
}
