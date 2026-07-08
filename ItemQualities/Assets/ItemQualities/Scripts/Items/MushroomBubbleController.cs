using EntityStates.MushroomShield;
using RoR2;
using UnityEngine;

namespace ItemQualities.Items
{
    public sealed class MushroomBubbleController : MonoBehaviour
    {
        private GenericOwnership _genericOwnership;

        private IgnoredCollisionsProvider _ignoredCollisionsProvider;

        private EntityStateMachine _stateMachine;

        private void Awake()
        {
            _genericOwnership = GetComponent<GenericOwnership>();
            _ignoredCollisionsProvider = GetComponent<IgnoredCollisionsProvider>();
            _stateMachine = GetComponent<EntityStateMachine>();
        }

        private void OnEnable()
        {
            if (_genericOwnership)
            {
                _genericOwnership.onOwnerChanged += onOwnerChanged;
            }

            refreshCollisionWhitelist();
        }

        private void OnDisable()
        {
            if (_genericOwnership)
            {
                _genericOwnership.onOwnerChanged -= onOwnerChanged;
            }
        }

        private void onOwnerChanged(GameObject newOwner)
        {
            refreshCollisionWhitelist();
        }

        private void refreshCollisionWhitelist()
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

        private void invokeStateUndeploy(bool immediate)
        {
            if (_stateMachine && _stateMachine.state is MushroomBubbleBaseState mushroomBubbleState)
            {
                mushroomBubbleState.Undeploy(immediate);
            }
        }
    }
}
