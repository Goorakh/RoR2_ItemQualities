using UnityEditor;
using UnityEngine;

namespace ItemQualities
{
#if UNITY_EDITOR
    [ExecuteAlways]
    public sealed class OrbitTest : MonoBehaviour
    {
        public Transform OrbitTransform;

        public bool InvertDirection;

        [Min(0)]
        public float Distance = 1f;

        [Min(0)]
        public float Speed = 1f;

        [Range(-90f, 90f)]
        public float TiltAngle = 0f;

        [Range(-360f, 360f)]
        public float TiltOffset = 0f;

        void Update()
        {
            int directionSign = InvertDirection ? -1 : 1;

            // in radians
            float angle = Time.time * Speed * directionSign;

            Quaternion tiltRotation = Quaternion.AngleAxis(TiltOffset, Vector3.up) * Quaternion.AngleAxis(TiltAngle, Vector3.forward);

            Vector3 localPosition = tiltRotation * new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * Distance;
            Quaternion localRotation = tiltRotation * Quaternion.AngleAxis((angle + ((Mathf.PI / 2f) * directionSign)) * Mathf.Rad2Deg, Vector3.up);

            OrbitTransform.SetLocalPositionAndRotation(localPosition, localRotation);
        }

        void OnDrawGizmos()
        {
            // Your gizmo drawing thing goes here if required...

            // Ensure continuous Update calls.
            if (!Application.isPlaying)
            {
                UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
                UnityEditor.SceneView.RepaintAll();
            }
        }
    }
#endif
}
