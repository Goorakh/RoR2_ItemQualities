using System;
using UnityEngine.Networking;

namespace EntityStates.MushroomShield
{
    public sealed class MushroomBubbleUndeploy : MushroomBubbleBaseState
    {
        [NonSerialized]
        public float Duration;

        public override void OnSerialize(NetworkWriter writer)
        {
            base.OnSerialize(writer);
            writer.Write(Duration);
        }

        public override void OnDeserialize(NetworkReader reader)
        {
            base.OnDeserialize(reader);
            Duration = reader.ReadSingle();
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();

            if (isAuthority && fixedAge >= Duration - MushroomBubbleFlashOut.Duration)
            {
                Undeploy(true);
            }
        }

        public override void Undeploy(bool immediate)
        {
            if (!isAuthority)
                return;

            if (immediate)
            {
                outer.SetNextState(new MushroomBubbleFlashOut());
            }
        }
    }
}
