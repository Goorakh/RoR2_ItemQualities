using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace ItemQualities.Items
{
    public class SprintArmorQualityItemBehavior : MonoBehaviour
    {
        public const float MaxSprintDeviationDistance = 0.3f;
        public const float RequiredSprintDuration = 1f;

        CharacterBody _body;

        bool _wasSprinting = false;

        float _sprintingStopwatch = 0f;
        Plane _sprintPlane = default;

        void Awake()
        {
            _body = GetComponent<CharacterBody>();
        }

        void OnDisable()
        {
            updateSprinting(false);
            setProvidingBuff(false);
        }

        void FixedUpdate()
        {
            if (!NetworkServer.active)
                return;

            updateSprinting(_body.isSprinting);

            if (_body.isSprinting)
            {
                _sprintingStopwatch += Time.fixedDeltaTime;
                if (Mathf.Abs(_sprintPlane.GetDistanceToPoint(_body.footPosition)) >= MaxSprintDeviationDistance)
                {
                    restartSprintTracking();
                }
            }

            setProvidingBuff(_body.isSprinting && _sprintingStopwatch >= RequiredSprintDuration);
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

            Vector3 startPosition = _body.footPosition;

            Vector3 sprintDirection = _body.transform.forward;

            CharacterDirection characterDirection = _body.characterDirection;
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
            if (providingBuff != _body.HasBuff(ItemQualitiesContent.Buffs.SprintArmorStrong))
            {
                if (providingBuff)
                {
                    _body.AddBuff(ItemQualitiesContent.Buffs.SprintArmorStrong);
                }
                else
                {
                    _body.RemoveBuff(ItemQualitiesContent.Buffs.SprintArmorStrong);
                }
            }
        }
    }
}
