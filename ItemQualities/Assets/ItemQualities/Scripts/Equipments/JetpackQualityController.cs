using RoR2;
using UnityEngine;
using UnityEngine.Networking;

using Random = UnityEngine.Random;

namespace ItemQualities.Equipments
{
    [RequireComponent(typeof(NetworkedBodyAttachment))]
    public sealed class JetpackQualityController : MonoBehaviour
    {
        NetworkedBodyAttachment _bodyAttachment;

        int _bugsPerPickup;

        float _pickupSpawnInterval;
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
                        _bugsPerPickup = 2;
                        _pickupSpawnInterval = 4f;
                        break;
                    case QualityTier.Rare:
                        _bugsPerPickup = 3;
                        _pickupSpawnInterval = 4f;
                        break;
                    case QualityTier.Epic:
                        _bugsPerPickup = 4;
                        _pickupSpawnInterval = 3f;
                        break;
                    case QualityTier.Legendary:
                        _bugsPerPickup = 5;
                        _pickupSpawnInterval = 3f;
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
            if (_pickupSpawnTimer >= _pickupSpawnInterval)
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

            IPhysMotor bodyMotor = body.characterMotor ? body.characterMotor : body.GetComponent<IPhysMotor>();

            Vector3 bodyForward = body.transform.forward;
            if (bodyMotor != null && bodyMotor.velocity.sqrMagnitude > Mathf.Epsilon)
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

            float sphereCastRadius = Mathf.Max(2f, body.bestFitActualRadius);

            const float MinApproximatePickupDistance = 25f;
            const float MaxApproximatePickupDistance = 50f;

            const float MinAcceptablePickupDistance = 8f;
            const float MinAcceptablePickupDistanceSqr = MinAcceptablePickupDistance * MinAcceptablePickupDistance;

            const int NumSteps = 4;

            const float StepSizeMin = MinApproximatePickupDistance / NumSteps;
            const float StepSizeMax = MaxApproximatePickupDistance / NumSteps;

#if DEBUG
            WireMeshBuilder pathMeshBuilder = Configs.Debug.EnableDebugDraw ? new WireMeshBuilder() : null;
#endif

            const int RetryLimit = 10;
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
                    if (Physics.SphereCast(ray, sphereCastRadius, out RaycastHit hit, stepSize, LayerIndex.world.mask, QueryTriggerInteraction.Ignore))
                    {
                        nextStepPosition = hit.point - (ray.direction * sphereCastRadius);
                        nextStepDirection = Vector3.Reflect(direction, hit.normal);
                    }
                    else
                    {
                        nextStepPosition = ray.GetPoint(stepSize - sphereCastRadius);
                        nextStepDirection = direction;
                    }

#if DEBUG
                    pathMeshBuilder?.AddLine(currentPosition, Color.yellow, nextStepPosition, Color.yellow);
#endif

                    currentPosition = nextStepPosition;
                    direction = nextStepDirection;
                }

                float sqrDistance = (currentPosition - body.corePosition).sqrMagnitude;
                if (sqrDistance > MinAcceptablePickupDistanceSqr)
                {
                    for (int i = 0; i < _bugsPerPickup; i++)
                    {
                        GameObject bugPickupObj = Instantiate(ItemQualitiesContent.NetworkedPrefabs.BugPickup, currentPosition, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));

                        TeamFilter teamFilter = bugPickupObj.GetComponent<TeamFilter>();
                        teamFilter.teamIndex = body.teamComponent.teamIndex;

                        BugPickup bugPickup = bugPickupObj.GetComponentInChildren<BugPickup>();
                        bugPickup.Tier = _activeQualityTier;

                        NetworkServer.Spawn(bugPickupObj);
                    }

                    break;
                }
            }

#if DEBUG
            if (pathMeshBuilder != null)
            {
                DebugOverlay.MeshDrawer pathMeshDrawer = DebugOverlay.GetMeshDrawer();
                pathMeshDrawer.gameObject.AddComponent<DestroyOnTimer>().duration = _pickupSpawnInterval;

                pathMeshDrawer.mesh = pathMeshBuilder.GenerateMesh();
            }
#endif
        }
    }
}
