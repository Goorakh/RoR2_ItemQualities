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

            if (fixedAge >= Duration - MushroomBubbleFlashOut.Duration && isAuthority)
            {
                outer.SetNextState(new MushroomBubbleFlashOut());
            }
        }
    }
}
