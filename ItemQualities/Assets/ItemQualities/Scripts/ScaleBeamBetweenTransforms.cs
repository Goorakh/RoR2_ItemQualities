using UnityEngine;

namespace ItemQualities
{
    public sealed class ScaleBeamBetweenTransforms : MonoBehaviour
    {
        public Transform aimTransform;

        public Transform scaleTransform;

        public Transform targetTransform;

        private void LateUpdate()
        {
            aimTransform.rotation = Quaternion.LookRotation(targetTransform.position - aimTransform.position, aimTransform.up);

            Vector3 toTarget = targetTransform.position - scaleTransform.position;

            Vector3 scale = scaleTransform.localScale;
            scale.z = toTarget.magnitude / transform.localScale.z;
            scaleTransform.localScale = scale;
        }

        private void OnValidate()
        {
            if (!aimTransform)
            {
                aimTransform = transform;
            }

            if (!scaleTransform)
            {
                scaleTransform = aimTransform;
            }
        }
    }
}
