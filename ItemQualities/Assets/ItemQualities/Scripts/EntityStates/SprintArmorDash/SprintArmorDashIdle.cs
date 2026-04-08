using ItemQualities;
using RoR2;

namespace EntityStates.SprintArmorDash
{
    public sealed class SprintArmorDashIdle : EntityState
    {
        public static float DoubleTapWindow;

        float _lastValidInputTime = float.NegativeInfinity;

        CharacterBody _attachedBody;

        public override void OnEnter()
        {
            base.OnEnter();

            NetworkedBodyAttachment networkedBodyAttachment = GetComponent<NetworkedBodyAttachment>();
            if (!networkedBodyAttachment || !networkedBodyAttachment.attachedBody)
                return;

            _attachedBody = networkedBodyAttachment.attachedBody;
        }

        public override void Update()
        {
            base.Update();

            if (!_attachedBody)
                return;

            if (isAuthority)
            {
                UpdateAuthority();
            }
        }

        void UpdateAuthority()
        {
            if (!_attachedBody.HasBuff(ItemQualitiesContent.Buffs.SprintArmorDashCooldown) && _attachedBody.inputBank.rawMoveUp.justPressed)
            {
                float timeSinceLastInput = age - _lastValidInputTime;
                if (timeSinceLastInput <= DoubleTapWindow)
                {
                    outer.SetNextState(new SprintArmorDashDashingState());
                }

                _lastValidInputTime = age;
            }
        }
    }
}
