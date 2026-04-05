using HG;
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

        bool _hasTeleported;

        public bool IsAvailable => !_hasTeleported;

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
            if (_hasTeleported)
                return;

            if (body.TryGetComponent(out IPhysMotor motor))
            {
                motor.velocityAuthority = motor.velocityAuthority.XAZ(0f);
            }

            Vector3 teleportPosition = transform.position;

            TeleportHelper.TeleportBody(new TeleportHelper.TeleportBodyArgs
            {
                body = body,
                forceOutOfVehicle = true,
                resetStateMachines = false,
                targetPosition = teleportPosition,
                targetRotation = body.transform.rotation,
            });

            GameObject teleportEffectPrefab = Run.instance.GetTeleportEffectPrefab(body.gameObject);
            if (teleportEffectPrefab)
            {
                EffectManager.SpawnEffect(teleportEffectPrefab, new EffectData
                {
                    origin = teleportPosition
                }, true);
            }

            _hasTeleported = true;

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
