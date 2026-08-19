using UnityEngine;
using UnityEngine.AddressableAssets;

namespace ItemQualities
{
    public sealed class AddressableMeshPreview : MonoBehaviour
    {
        [SerializeField]
        private AssetReferenceT<Mesh> _meshReference = new AssetReferenceT<Mesh>(string.Empty);

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (_meshReference != null && _meshReference.RuntimeKeyIsValid())
            {
                Mesh mesh;
                if (!_meshReference.OperationHandle.IsValid())
                {
                    mesh = _meshReference.LoadAssetAsync().WaitForCompletion();
                }
                else
                {
                    mesh = _meshReference.OperationHandle.Convert<Mesh>().Result;
                }

                if (mesh)
                {
                    Gizmos.matrix = transform.localToWorldMatrix;
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawWireMesh(mesh);
                }
            }
        }
#endif
    }
}
