using UnityEngine;

namespace ItemQualities
{
    public class ConstantBoneOffset : MonoBehaviour
    {
        public Vector3 PositionOffset;

        [SerializeField]
        Animator _animator;

        Vector3 _lastLocalPosition;

        void OnEnable()
        {
            _lastLocalPosition = Vector3.positiveInfinity;
        }

        void LateUpdate()
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
