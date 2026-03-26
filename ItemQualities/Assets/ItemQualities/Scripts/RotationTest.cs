using UnityEngine;

namespace ItemQualities
{
#if UNITY_EDITOR
    public sealed class RotationTest : MonoBehaviour
    {
        public float Pitch;

        public float Yaw;

        void OnDrawGizmos()
        {
            Quaternion rotation = Quaternion.Euler(Pitch, Yaw, 0f);

            Vector3 forward = rotation * Vector3.forward;
            Gizmos.DrawLine(Vector3.zero, forward);
        }
    }
#endif
}
