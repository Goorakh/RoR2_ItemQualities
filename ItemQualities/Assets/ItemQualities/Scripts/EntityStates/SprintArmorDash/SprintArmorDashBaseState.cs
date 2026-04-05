using ItemQualities;
using RoR2;
using UnityEngine;

namespace EntityStates.SprintArmorDash
{
    public class SprintArmorDashBaseState : EntityState
    {
        bool _heldForward;
        float _timer = 0;
        CharacterBody _attachedBody;

        public override void OnEnter()
        {
            base.OnEnter();
            NetworkedBodyAttachment networkedBodyAttachment = GetComponent<NetworkedBodyAttachment>();
            if (!networkedBodyAttachment || !networkedBodyAttachment.attachedBody)
                return;
            _attachedBody = networkedBodyAttachment.attachedBody;
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();
            if (!_attachedBody || !base.isAuthority)
                return;
            _timer += GetDeltaTime();
            Vector3 moveVector = _attachedBody.inputBank.moveVector;
            Vector3 aimVector = _attachedBody.inputBank.aimDirection;
            aimVector.y = 0;
            float angleDiff = Vector3.Angle(moveVector.normalized, aimVector);

            if (!_attachedBody.HasBuff(ItemQualitiesContent.Buffs.SprintArmorDashCooldown) &&
            angleDiff < 70 && moveVector.magnitude > 0.2)
            {
                if (!_heldForward)
                {
                    _heldForward = true;
                    if (_timer < 0.2f)
                    {
                        outer.SetNextState(new SprintArmorDashDashingState());
                    }
                    else
                    {
                        _timer = 0f;
                    }
                }
            }
            else
            {
                _heldForward = false;
            }
        }
    }
}
