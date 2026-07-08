using HG;
using ItemQualities.Utilities.Extensions;
using RoR2;
using System;
using UnityEngine;

using Random = UnityEngine.Random;

namespace ItemQualities
{
    [RequireComponent(typeof(NetworkedBodyAttachment))]
    public sealed class BugSwarmController : MonoBehaviour, INetworkedBodyAttachmentListener
    {
        [Tooltip("This will be instantiated and circled around the attached body")]
        public GameObject BugPrefab;

        [Min(1)]
        [Tooltip("The maximum amount of bugs to be active at a time")]
        public int MaxDisplayedBugCount = 20;

        [Header("Orbit Parameters")]

        [Tooltip("The min and max offsets of the distance from the attached body to orbit")]
        public RangeFloat OrbitDistanceOffset = new RangeFloat { min = 0f, max = 0.2f };

        [Tooltip("The min and max speed of bugs in orbit (in revolutions/sec)")]
        public RangeFloat OrbitSpeed = new RangeFloat { min = 0.5f, max = 1f };

        [Tooltip("The min and max angle tilt of the bugs orbit (in degrees)")]
        public RangeFloat OrbitTiltAngle = new RangeFloat { min = 0f, max = 35f };

        [Header("Orbit Parameters - Offsets")]

        [Tooltip("The min and max vertical offset to cycle in the orbit")]
        public RangeFloat OrbitVerticalOffsetMagnitude = new RangeFloat { min = 0.1f, max = 0.2f };

        [Tooltip("The min and max frequency of the vertical offset in the orbit")]
        public RangeFloat OrbitVerticalOffsetFrequency = new RangeFloat { min = 1f, max = 1.5f };

        [Header("Bug Parameters")]

        [Min(0f)]
        [Tooltip("How long to smooth the position of a bug to its target position")]
        public float PositionSmoothDuration = 0.4f;

        [Min(0f)]
        [Tooltip("How long to smooth the rotation of a bug to its target rotation")]
        public float RotationSmoothDuration = 0.2f;

        private int _bugCount;
        private BugOrbit[] _bugs = Array.Empty<BugOrbit>();

        private NetworkedBodyAttachment _bodyAttachment;

        private CharacterBody _attachedBody;

        private void Awake()
        {
            _bodyAttachment = GetComponent<NetworkedBodyAttachment>();
        }

        private void OnEnable()
        {
            if (_bodyAttachment)
            {
                setAttachedBody(_bodyAttachment.attachedBody);
            }
        }

        private void OnDisable()
        {
            setAttachedBody(null);
        }

        private void setAttachedBody(CharacterBody body)
        {
            if (_attachedBody == body)
                return;

            if (_attachedBody)
            {
                _attachedBody.onRecalculateStats -= onAttachedBodyRecalculateStats;
            }

            _attachedBody = body;

            if (_attachedBody)
            {
                _attachedBody.onRecalculateStats += onAttachedBodyRecalculateStats;
            }

            updateAttachedBodyBuffs();
        }

        private void onAttachedBodyRecalculateStats(CharacterBody body)
        {
            updateAttachedBodyBuffs();
        }

        private void updateAttachedBodyBuffs()
        {
            int bugInstanceCount = _attachedBody ? _attachedBody.GetBuffCounts(ItemQualitiesContent.BuffQualityGroups.BugBlock).TotalQualityCount : 0;
            setActiveBugCount(bugInstanceCount);
        }

        private void setActiveBugCount(int newBugCount)
        {
            newBugCount = Math.Clamp(newBugCount, 0, MaxDisplayedBugCount);

            if (newBugCount == _bugCount)
                return;

            int bugCountDiff = newBugCount - _bugCount;
            if (bugCountDiff < 0)
            {
                for (int i = _bugCount - 1; i >= newBugCount; i--)
                {
                    ref BugOrbit bug = ref _bugs[i];
                    if (bug.BugTransform)
                    {
                        bug.BugTransform.gameObject.SetActive(false);
                    }
                }
            }
            else
            {
                ArrayUtils.EnsureCapacity(ref _bugs, newBugCount);

                for (int i = _bugCount; i < newBugCount; i++)
                {
                    ref BugOrbit bug = ref _bugs[i];
                    if (bug.BugTransform)
                    {
                        bug.BugTransform.gameObject.SetActive(true);
                    }
                    else
                    {
                        GameObject gameObject1 = Instantiate(BugPrefab, transform);
                        bug.BugTransform = gameObject1.transform;
                    }

                    initializeBugInstance(ref bug);
                }
            }

            _bugCount += bugCountDiff;
        }

        private void initializeBugInstance(ref BugOrbit bug)
        {
            bug.DistanceOffset = Random.Range(OrbitDistanceOffset.min, OrbitDistanceOffset.max);

            bug.RadiansPerSecond = Random.Range(OrbitSpeed.min, OrbitSpeed.max) * (2f * Mathf.PI);

            bug.CycleOffset = Random.Range(0f, 2f * Mathf.PI);

            float tiltAngle = Random.Range(OrbitTiltAngle.min, OrbitTiltAngle.max) * (Random.value > 0.5f ? 1 : -1);
            float tiltOffset = Random.Range(0f, 360f);

            bug.TiltRotation = Quaternion.AngleAxis(tiltOffset, Vector3.up) * Quaternion.AngleAxis(tiltAngle, Vector3.forward);

            bug.DirectionSign = Random.value > 0.5f ? 1 : -1;

            bug.VerticalOffset = new Wave
            {
                amplitude = Random.Range(OrbitVerticalOffsetMagnitude.min, OrbitVerticalOffsetMagnitude.max),
                frequency = Random.Range(OrbitVerticalOffsetFrequency.min, OrbitVerticalOffsetFrequency.max),
                cycleOffset = Random.Range(0f, 2f * Mathf.PI),
            };

            bug.PositionSmoothVelocity = Vector3.zero;
            bug.RotationSmoothVelocity = 0f;
        }

        private void FixedUpdate()
        {
            if (_bugCount == 0)
                return;

            float baseOrbitDistance;
            Vector3 centerPosition;
            if (_attachedBody)
            {
                baseOrbitDistance = _attachedBody.radius * 1.3f;
                centerPosition = _attachedBody.corePosition;
            }
            else
            {
                baseOrbitDistance = 1f;
                centerPosition = transform.position;
            }

            for (int i = 0; i < _bugCount; i++)
            {
                ref BugOrbit bug = ref _bugs[i];
                if (!bug.BugTransform)
                    continue;

                // angle in radians
                float t = ((Time.fixedTime * bug.RadiansPerSecond) + bug.CycleOffset) * bug.DirectionSign;

                float orbitDistance = baseOrbitDistance + bug.DistanceOffset;
                Vector3 orbitVector = new Vector3(Mathf.Sin(t), 0f, Mathf.Cos(t)) * orbitDistance;

                float verticalOffset = bug.VerticalOffset.Evaluate(t);
                orbitVector += new Vector3(0f, verticalOffset, 0f);

                Vector3 targetPosition = centerPosition + (bug.TiltRotation * orbitVector);

                float directionQuarterRotation = (Mathf.PI / 2f) * bug.DirectionSign;
                Quaternion targetRotation = bug.TiltRotation * Quaternion.AngleAxis((t + directionQuarterRotation) * Mathf.Rad2Deg, Vector3.up);

                Vector3 position = Vector3.SmoothDamp(bug.BugTransform.position, targetPosition, ref bug.PositionSmoothVelocity, PositionSmoothDuration);
                Quaternion rotation = Util.SmoothDampQuaternion(bug.BugTransform.rotation, targetRotation, ref bug.RotationSmoothVelocity, RotationSmoothDuration);

                bug.BugTransform.SetPositionAndRotation(position, rotation);
            }
        }

        void INetworkedBodyAttachmentListener.OnAttachedBodyDiscovered(NetworkedBodyAttachment networkedBodyAttachment, CharacterBody attachedBody)
        {
            setAttachedBody(attachedBody);
        }

        private struct BugOrbit
        {
            public Transform BugTransform;

            public float RadiansPerSecond;

            public float DistanceOffset;

            public int DirectionSign;

            public float CycleOffset;

            public Quaternion TiltRotation;

            public Wave VerticalOffset;

            public Vector3 PositionSmoothVelocity;

            public float RotationSmoothVelocity;
        }
    }
}
