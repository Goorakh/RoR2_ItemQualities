using ItemQualities;
using ItemQualities.ModCompatibility;
using RoR2;

namespace EntityStates.SprintArmorDash
{
    public sealed class SprintArmorDashIdle : EntityState
    {
        public static float DoubleTapWindow;

        private float _lastValidInputTime = float.NegativeInfinity;

        private CharacterBody _attachedBody;
        private new InputBankTest inputBank;

        private bool canDash => _attachedBody && !_attachedBody.HasBuff(ItemQualitiesContent.Buffs.SprintArmorDashCooldown);

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

            if (_attachedBody.hasEffectiveAuthority)
            {
                UpdateAuthority();
            }
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();

            if (!_attachedBody)
                return;

            if (_attachedBody.hasEffectiveAuthority)
            {
                FixedUpdateAuthority();
            }
        }

        private void UpdateAuthority()
        {
            if (canDash && inputBank.interact.down)
            {
                if (inputBank.rawMoveUp.justPressed)
                {
                    float timeSinceLastInput = age - _lastValidInputTime;
                    if (timeSinceLastInput <= DoubleTapWindow)
                    {
                        StartDashAuthority();
                    }

                    _lastValidInputTime = age;
                }
            }
            else
            {
                _lastValidInputTime = float.NegativeInfinity;
            }
        }

        private void FixedUpdateAuthority()
        {
            if (canDash && RebindablesCompat.GetSprintArmorDashButtonState(inputBank).justPressed)
            {
                StartDashAuthority();
            }
        }

        private void StartDashAuthority()
        {
            outer.SetNextState(new SprintArmorDashDashingState());
        }
    }
}
