using EntityStates.MushroomShield;
using RoR2;
using UnityEngine;

namespace ItemQualities.Items
{
    public sealed class MushroomBubbleController : MonoBehaviour
    {
        GenericOwnership _genericOwnership;

        IgnoredCollisionsProvider _ignoredCollisionsProvider;

        EntityStateMachine _stateMachine;

        void Awake()
        {
            _genericOwnership = GetComponent<GenericOwnership>();
            _ignoredCollisionsProvider = GetComponent<IgnoredCollisionsProvider>();
            _stateMachine = GetComponent<EntityStateMachine>();
        }

        void OnEnable()
        {
            if (_genericOwnership)
            {
                _genericOwnership.onOwnerChanged += onOwnerChanged;
            }

            refreshCollisionWhitelist();
        }

        void OnDisable()
        {
            if (_genericOwnership)
            {
                _genericOwnership.onOwnerChanged -= onOwnerChanged;
            }
        }

        void onOwnerChanged(GameObject newOwner)
        {
            refreshCollisionWhitelist();
        }

        void refreshCollisionWhitelist()
        {
            GameObject ownerObject = _genericOwnership ? _genericOwnership.ownerObject : null;
            TeamIndex ownerTeam = TeamComponent.GetObjectTeam(ownerObject);

            if (_ignoredCollisionsProvider)
            {
                _ignoredCollisionsProvider.CollisionWhitelistFilter = ownerTeam != TeamIndex.None ? new TeamObjectFilter(ownerTeam) { InvertFilter = true } : null;
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
