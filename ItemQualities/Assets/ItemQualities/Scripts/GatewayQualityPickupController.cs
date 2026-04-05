using ItemQualities.Networking;
using R2API.Networking;
using R2API.Networking.Interfaces;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace ItemQualities
{
    [RequireComponent(typeof(TeamFilter))]
    public sealed class GatewayQualityPickupController : NetworkBehaviour
    {
        public TeamFilter TeamFilter;

        public Transform CoreTransform;

        bool _hasTeleportedAuthority;

        public bool IsAvailable => !_hasTeleportedAuthority;

        void Awake()
        {
            TeamFilter = GetComponent<TeamFilter>();
        }

        void OnEnable()
        {
            InstanceTracker.Add(this);
        }

        void OnDisable()
        {
            InstanceTracker.Remove(this);
        }

        public void OnInteractAuthority(CharacterBody body)
        {
            if (_hasTeleportedAuthority)
                return;

            TeleportHelper.TeleportBody(new TeleportHelper.TeleportBodyArgs
            {
                body = body,
                forceOutOfVehicle = true,
                resetStateMachines = false,
                targetPosition = transform.position,
                targetRotation = body.transform.rotation,
                teleportMinions = true
            });

            _hasTeleportedAuthority = true;

            if (NetworkServer.active)
            {
                OnTeleportServer();
            }
            else
            {
                new GatewayPickupTeleportMessage(gameObject).Send(NetworkDestination.Server);
            }
        }

        [Server]
        public void OnTeleportServer()
        {
            Destroy(gameObject);
        }
    }
}
