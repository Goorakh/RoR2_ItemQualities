using ItemQualities.Equipments;
using RoR2;

namespace EntityStates.QuestVolatileBatteryQuality
{
    public abstract class QuestVolatileBatteryQualityBaseState : EntityState
    {
        protected CharacterBody attachedBody { get; private set; }

        public override void OnEnter()
        {
            base.OnEnter();

            if (TryGetComponent(out QuestVolatileBatteryAttachment questVolatileBatteryAttachment))
            {
                attachedBody = questVolatileBatteryAttachment.victimBody;
            }
        }
    }
}
