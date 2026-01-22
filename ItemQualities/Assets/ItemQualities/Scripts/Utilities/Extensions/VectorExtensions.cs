using UnityEngine;

namespace ItemQualities.Utilities.Extensions
{
    internal static class VectorExtensions
    {
        public static Vector2 Inverse(this in Vector2 vector)
        {
            return new Vector2(1f / vector.x, 1f / vector.y);
        }

        public static Vector3 Inverse(this in Vector3 vector)
        {
            return new Vector3(1f / vector.x, 1f / vector.y, 1f / vector.z);
        }

        public static Vector4 Inverse(this in Vector4 vector)
        {
            return new Vector4(1f / vector.x, 1f / vector.y, 1f / vector.z, 1f / vector.w);
        }
    }
}
