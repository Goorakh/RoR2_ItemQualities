using UnityEngine;

namespace ItemQualities
{
    public sealed class RandomBugFlyMovement : MonoBehaviour
    {
        Vector3 _basePosition;

        [Tooltip("The maximum distance from the center position the bug can move")]
        public float MaxDistance;

        [Tooltip("The minimum amount of time to stay in any given position")]
        public float PauseDurationMin;

        [Tooltip("The maximum amount of time to stay in any given position")]
        public float PauseDurationMax;

        [Tooltip("How fast to move towards a new position")]
        public float MoveSpeed;

        [Tooltip("If set, this object's forward vector will be set to match the current movement direction")]
        public bool FaceMoveDirection;

        public AnimationCurve MoveCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        // global state
        State _currentState;
        float _timer;

        // moving state
        float _moveTotalDuration;
        Vector3 _lastPosition;
        Vector3 _targetPosition;

        void OnEnable()
        {
            _basePosition = transform.localPosition;
            setState(State.Idle);
        }

        void OnDisable()
        {
            transform.localPosition = _basePosition;
        }

        void FixedUpdate()
        {
            switch (_currentState)
            {
                case State.Idle:
                    _timer -= Time.fixedDeltaTime;
                    if (_timer <= 0f)
                    {
                        setState(State.Moving);
                    }

                    break;
                case State.Moving:
                    _timer -= Time.fixedDeltaTime;

                    float t = MoveCurve.Evaluate(1f - (_timer / _moveTotalDuration));
                    transform.localPosition = Vector3.Lerp(_lastPosition, _targetPosition, t);

                    if (_timer <= 0f)
                    {
                        setState(State.Idle);
                    }

                    break;
            }
        }

        void setState(State state)
        {
            _currentState = state;

            switch (_currentState)
            {
                case State.Idle:
                    _timer = Random.Range(PauseDurationMin, PauseDurationMax);
                    break;
                case State.Moving:
                    _lastPosition = transform.localPosition;
                    _targetPosition = _basePosition + (Random.insideUnitSphere * MaxDistance);
                    Vector3 moveVector = (_lastPosition - _targetPosition);
                    _moveTotalDuration = moveVector.magnitude / MoveSpeed;
                    _timer = _moveTotalDuration;

                    if (FaceMoveDirection)
                    {
                        Vector3 moveVectorHorizontal = moveVector;
                        moveVectorHorizontal.y = 0f;
                        transform.forward = transform.TransformDirection(moveVectorHorizontal.normalized);
                    }

                    break;
            }
        }

        enum State : byte
        {
            Idle,
            Moving
        }
    }
}
