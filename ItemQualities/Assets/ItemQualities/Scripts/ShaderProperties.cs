using UnityEngine;

namespace ItemQualities
{
    static class ShaderProperties
    {
        public static readonly int _Color = Shader.PropertyToID("_Color");

        public static readonly int _TintColor = Shader.PropertyToID("_TintColor");

        public static readonly int _EmissionColor = Shader.PropertyToID("_EmColor");

        public static readonly int _Smoothness = Shader.PropertyToID("_Smoothness");

        public static readonly int _SpecularStrength = Shader.PropertyToID("_SpecularStrength");

        public static readonly int _SpecularExponent = Shader.PropertyToID("_SpecularExponent");

        public static readonly int _FresnelEmissionEnabled = Shader.PropertyToID("_FEON");
    }
}
