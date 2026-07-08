using RoR2;
using UnityEngine;

namespace ItemQualities
{
    public sealed class AxisBillboard : MonoBehaviour
    {
        public Vector3 Axis = Vector3.up;

        private void OnEnable()
        {
            SceneCamera.onSceneCameraPreCull += onSceneCameraPreCull;
        }

        private void OnDisable()
        {
            SceneCamera.onSceneCameraPreCull -= onSceneCameraPreCull;
        }

        private void onSceneCameraPreCull(SceneCamera sceneCamera)
        {
            Vector3 position = Vector3.ProjectOnPlane(transform.position, Axis);
            Vector3 cameraPosition = Vector3.ProjectOnPlane(sceneCamera.transform.position, Axis);

            transform.rotation = Util.QuaternionSafeLookRotation((cameraPosition - position).normalized);
        }
    }
}
