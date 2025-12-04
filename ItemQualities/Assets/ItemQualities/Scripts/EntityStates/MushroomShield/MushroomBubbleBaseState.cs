using System;
using UnityEngine.Networking;

namespace EntityStates.MushroomShield
{
    public abstract class MushroomBubbleBaseState : EntityState
    {
        [NonSerialized]
        public float EffectRadius;

        public override void OnSerialize(NetworkWriter writer)
        {
            base.OnSerialize(writer);
            writer.Write(EffectRadius);
        }

        public override void OnDeserialize(NetworkReader reader)
        {
            base.OnDeserialize(reader);
            EffectRadius = reader.ReadSingle();
        }

        public override void ModifyNextState(EntityState nextState)
        {
            base.ModifyNextState(nextState);

            if (nextState is MushroomBubbleBaseState bubbleState)
            {
                bubbleState.EffectRadius = EffectRadius;
            }
        }

        public abstract void Undeploy(bool immediate);
    }
}
