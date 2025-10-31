using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace ItemQualities
{
    public sealed class GenericDurationComponent : NetworkBehaviour
    {
        [SyncVar(hook = nameof(hookSetDuration))]
        public float Duration;

        public BuffWard BuffWard;

        public BeginRapidlyActivatingAndDeactivating BlinkController;

        public float BlinkDuration = 1f;

        public override void OnStartClient()
        {
            setDuration();
        }

        void setDuration()
        {
            if (BuffWard)
            {
                BuffWard.expireDuration = Duration;
            }

            if (BlinkController)
            {
                BlinkController.delayBeforeBeginningBlinking = Mathf.Max(0f, Duration - BlinkDuration);
            }
        }

        void hookSetDuration(float duration)
        {
            Duration = duration;
            setDuration();
        }
    }
}
