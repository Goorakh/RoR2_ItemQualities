using RoR2;
using RoR2.ConVar;
using UnityEngine;
using UnityEngine.Networking;

using Random = UnityEngine.Random;

namespace ItemQualities.Equipments
{
    [RequireComponent(typeof(NetworkedBodyAttachment))]
    public sealed class JetpackQualityController : MonoBehaviour
    {
        NetworkedBodyAttachment _bodyAttachment;

        float _pickupSpawnWaitTime;
        float _pickupSpawnTimer;

        QualityTier _activeQualityTier = QualityTier.None;
        public QualityTier ActiveQualityTier
        {
            get => _activeQualityTier;
            set
            {
                if (_activeQualityTier == value)
                    return;

                _activeQualityTier = value;
                switch (_activeQualityTier)
                {
                    case QualityTier.None:
                        break;
                    case QualityTier.Uncommon:
                        _pickupSpawnWaitTime = 6f;
                        break;
                    case QualityTier.Rare:
                        _pickupSpawnWaitTime = 5f;
                        break;
                    case QualityTier.Epic:
                        _pickupSpawnWaitTime = 4f;
                        break;
                    case QualityTier.Legendary:
                        _pickupSpawnWaitTime = 3f;
                        break;
                    default:
                        Log.Warning($"Quality tier {_activeQualityTier} is not implemented");
                        break;
                }
            }
        }

        void Awake()
        {
            _bodyAttachment = GetComponent<NetworkedBodyAttachment>();
        }

        void FixedUpdate()
        {
            if (!NetworkServer.active || _activeQualityTier == QualityTier.None)
                return;

            _pickupSpawnTimer += Time.fixedDeltaTime;
            if (_pickupSpawnTimer >= _pickupSpawnWaitTime)
            {
                _pickupSpawnTimer = 0f;
                trySpawnNearbyPickup();
            }
        }

        void trySpawnNearbyPickup()
        {
            if (!_bodyAttachment)
                return;

            CharacterBody body = _bodyAttachment.attachedBody;
            if (!body)
                return;

            Vector3 bodyForward = body.transform.forward;
            if (body.TryGetComponent(out IPhysMotor bodyMotor) && bodyMotor.velocity.sqrMagnitude > Mathf.Epsilon)
            {
                bodyForward = bodyMotor.velocity.normalized;
            }
            else if (body.characterDirection)
            {
                if (body.characterDirection.moveVector.sqrMagnitude > Mathf.Epsilon)
                {
                    bodyForward = body.characterDirection.moveVector.normalized;
                }
                else
                {
                    bodyForward = body.characterDirection.forward;
                }
            }

            const float MinPickupDistance = 25f;
            const float MaxPickupDistance = 50f;

            const float MinPickupDistanceSqr = MinPickupDistance * MinPickupDistance;

            const int NumSteps = 4;

            const float StepSizeMin = MinPickupDistance / NumSteps;
            const float StepSizeMax = MaxPickupDistance / NumSteps;

#if DEBUG
            WireMeshBuilder pathMeshBuilder = Configs.Debug.EnableDebugDraw ? new WireMeshBuilder() : null;
#endif

            const int RetryLimit = 5;
            for (int attemptNumber = 0; attemptNumber < RetryLimit; attemptNumber++)
            {
                Vector3 currentPosition = body.corePosition;
                Vector3 direction = bodyForward;

#if DEBUG
                pathMeshBuilder?.Clear();
#endif

                for (int step = 0; step < NumSteps; step++)
                {
                    direction = Quaternion.Euler(Random.Range(-30f, 30f), Random.Range(-45f, 45f), 0f) * direction;

                    float stepSizeT = Random.value;
                    stepSizeT *= stepSizeT;
                    float stepSize = Mathf.Lerp(StepSizeMin, StepSizeMax, stepSizeT);

                    Vector3 nextStepPosition;
                    Vector3 nextStepDirection;

                    Ray ray = new Ray(currentPosition, direction);
                    float radius = body.bestFitActualRadius;
                    if (Physics.SphereCast(ray, radius, out RaycastHit hit, stepSize, LayerIndex.world.mask, QueryTriggerInteraction.Ignore))
                    {
                        nextStepPosition = hit.point;
                        nextStepDirection = Vector3.Reflect(direction, hit.normal);
                    }
                    else
                    {
                        nextStepPosition = ray.GetPoint(stepSize - radius);
                        nextStepDirection = direction;
                    }

#if DEBUG
                    pathMeshBuilder?.AddLine(currentPosition, Color.yellow, nextStepPosition, Color.yellow);
#endif

                    currentPosition = nextStepPosition;
                    direction = nextStepDirection;
                }

                float sqrDistance = (currentPosition - body.corePosition).sqrMagnitude;
                if (sqrDistance > MinPickupDistanceSqr)
                {
                    GameObject bugPickupObj = Instantiate(ItemQualitiesContent.NetworkedPrefabs.BugPickup, currentPosition, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));

                    TeamFilter teamFilter = bugPickupObj.GetComponent<TeamFilter>();
                    teamFilter.teamIndex = body.teamComponent.teamIndex;

                    BugPickup bugPickup = bugPickupObj.GetComponentInChildren<BugPickup>();
                    bugPickup.Tier = _activeQualityTier;

                    NetworkServer.Spawn(bugPickupObj);

                    break;
                }
            }

#if DEBUG
            if (pathMeshBuilder != null)
            {
                DebugOverlay.MeshDrawer pathMeshDrawer = DebugOverlay.GetMeshDrawer();
                pathMeshDrawer.gameObject.AddComponent<DestroyOnTimer>().duration = _pickupSpawnWaitTime;

                pathMeshDrawer.mesh = pathMeshBuilder.GenerateMesh();
            }
#endif
        }
    }
}
