using UnityEngine;

namespace ItemQualities
{
    public sealed class ConstantBoneOffset : MonoBehaviour
    {
        public Vector3 PositionOffset;

        [SerializeField]
        private Animator _animator;

        private Vector3 _lastLocalPosition;

        private void OnEnable()
        {
            _lastLocalPosition = Vector3.positiveInfinity;
        }

        private void LateUpdate()
        {
            if (_animator && _animator.isActiveAndEnabled)
            {
                if ((transform.localPosition - _lastLocalPosition).sqrMagnitude >= 0.01f * 0.01f)
                {
                    transform.localPosition += PositionOffset;
                    _lastLocalPosition = transform.localPosition;
                }
            }
        }
    }
}
