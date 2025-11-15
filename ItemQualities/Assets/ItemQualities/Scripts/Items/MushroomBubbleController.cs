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
                deployable.onUndeploy.AddListener(undeploy);
            }
            else
            {
                Log.Error("Mushroom bubble is missing Deployable component");
            }
        }

        void undeploy()
        {
            if (_stateMachine && _stateMachine.state is not MushroomBubbleFlashOut)
            {
                _stateMachine.SetNextState(new MushroomBubbleFlashOut());
            }
        }
    }
}
