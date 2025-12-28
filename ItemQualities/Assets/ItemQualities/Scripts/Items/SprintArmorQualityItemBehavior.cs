using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace ItemQualities.Items
{
    public sealed class SprintArmorQualityItemBehavior : QualityItemBodyBehavior
    {
        [ItemGroupAssociation(QualityItemBehaviorUsageFlags.Server)]
        static ItemQualityGroup GetItemGroup()
        {
            return ItemQualitiesContent.ItemQualityGroups.SprintArmor;
        }

        public const float MaxSprintDeviationDistance = 0.3f;
        public const float RequiredSprintDuration = 1f;

        bool _wasSprinting = false;

        float _sprintingStopwatch = 0f;
        Plane _sprintPlane = default;

        void OnDisable()
        {
            updateSprinting(false);
            setProvidingBuff(false);
        }

        void FixedUpdate()
        {
            if (!NetworkServer.active)
                return;

            updateSprinting(Body.isSprinting);

            if (Body.isSprinting)
            {
                _sprintingStopwatch += Time.fixedDeltaTime;
                if (Mathf.Abs(_sprintPlane.GetDistanceToPoint(Body.footPosition)) >= MaxSprintDeviationDistance)
                {
                    restartSprintTracking();
                }
            }

            setProvidingBuff(Body.isSprinting && _sprintingStopwatch >= RequiredSprintDuration);
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

        void setProvidingBuff(bool providingBuff)
        {
            if (providingBuff != Body.HasBuff(ItemQualitiesContent.Buffs.SprintArmorStrong))
            {
                if (providingBuff)
                {
                    Body.AddBuff(ItemQualitiesContent.Buffs.SprintArmorStrong);
                }
                else
                {
                    Body.RemoveBuff(ItemQualitiesContent.Buffs.SprintArmorStrong);
                }
            }
        }
    }
}
