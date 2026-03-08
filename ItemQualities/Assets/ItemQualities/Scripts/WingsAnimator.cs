using RoR2;
using UnityEngine;

namespace ItemQualities
{
    public sealed class WingsAnimator : MonoBehaviour
    {
        [SerializeField]
        Animator _animator;

        public bool Ready = true;

        public float FlyRate = 4f;

        void OnEnable()
        {
            if (_animator)
            {
                _animator.SetBool(JetpackController.wingsReadyParamHash, Ready);
                _animator.SetFloat(JetpackController.flyParamHash, FlyRate);
            }
        }
    }
}
