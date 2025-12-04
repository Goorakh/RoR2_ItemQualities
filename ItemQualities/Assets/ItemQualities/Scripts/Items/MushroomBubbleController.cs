using EntityStates.MushroomShield;
using RoR2;
using UnityEngine;

namespace ItemQualities.Items
{
    public sealed class MushroomBubbleController : MonoBehaviour
    {
        EntityStateMachine _stateMachine;

        void Awake()
        {
            _stateMachine = GetComponent<EntityStateMachine>();

            if (TryGetComponent(out Deployable deployable))
            {
                deployable.onUndeploy.AddListener(UndeployImmediate);
            }
            else
            {
                Log.Error("Mushroom bubble is missing Deployable component");
            }
        }

        public void Undeploy()
        {
            invokeStateUndeploy(false);
        }

        public void UndeployImmediate()
        {
            invokeStateUndeploy(true);
        }

        void invokeStateUndeploy(bool immediate)
        {
            if (_stateMachine && _stateMachine.state is MushroomBubbleBaseState mushroomBubbleState)
            {
                mushroomBubbleState.Undeploy(immediate);
            }
        }
    }
}
