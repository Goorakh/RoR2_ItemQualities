using ItemQualities;
using RoR2;

namespace EntityStates.SprintArmorDash
{
    public sealed class SprintArmorDashIdle : EntityState
    {
        public static float DoubleTapWindow;

        private float _lastValidInputTime = float.NegativeInfinity;

        private CharacterBody _attachedBody;
        private new InputBankTest inputBank;

        public override void OnEnter()
        {
            base.OnEnter();

            NetworkedBodyAttachment networkedBodyAttachment = GetComponent<NetworkedBodyAttachment>();
            if (!networkedBodyAttachment || !networkedBodyAttachment.attachedBody)
                return;

            _attachedBody = networkedBodyAttachment.attachedBody;
            inputBank = _attachedBody.inputBank;
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

        private void UpdateAuthority()
        {
            if (!_attachedBody.HasBuff(ItemQualitiesContent.Buffs.SprintArmorDashCooldown) && inputBank.interact.down)
            {
                if (inputBank.rawMoveUp.justPressed)
                {
                    float timeSinceLastInput = age - _lastValidInputTime;
                    if (timeSinceLastInput <= DoubleTapWindow)
                    {
                        outer.SetNextState(new SprintArmorDashDashingState());
                    }

                    _lastValidInputTime = age;
                }
            }
            else
            {
                _lastValidInputTime = float.NegativeInfinity;
            }
        }
    }
}
