using HG;
using UnityEngine;

namespace ItemQualities.Utilities
{
    public static class MeshUtil
    {
        static Mesh[] _primitiveMeshCache = new Mesh[(int)PrimitiveType.Quad + 1];

        public static Mesh GetPrimitive(PrimitiveType primitiveType)
        {
            Mesh mesh = ArrayUtils.GetSafe(_primitiveMeshCache, (int)primitiveType);
            if (!mesh)
            {
                GameObject tempPrimitive = GameObject.CreatePrimitive(primitiveType);
                if (tempPrimitive)
                {
                    if (tempPrimitive.TryGetComponent(out MeshFilter meshFilter))
                    {
                        mesh = meshFilter.sharedMesh;
                        if (mesh)
                        {
                            ArrayUtils.EnsureCapacity(ref _primitiveMeshCache, (int)primitiveType + 1);
                            _primitiveMeshCache[(int)primitiveType] = mesh;
                        }
                    }

                    GameObject.Destroy(tempPrimitive);
                }
            }

            return mesh;
        }
    }
}
