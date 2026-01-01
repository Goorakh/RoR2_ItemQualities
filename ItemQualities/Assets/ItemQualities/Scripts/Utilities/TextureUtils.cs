using UnityEngine;

namespace ItemQualities.Utilities
{
    public static class TextureUtils
    {
        public static Texture2D CreateAccessibleCopy(Texture2D texture)
        {
            //https://forum.unity.com/threads/easy-way-to-make-texture-isreadable-true-by-script.1141915/
            RenderTexture renderTex = RenderTexture.GetTemporary(
                texture.width,
                texture.height,
                0,
                RenderTextureFormat.ARGB32,
                texture.isDataSRGB ? RenderTextureReadWrite.sRGB : RenderTextureReadWrite.Linear);

            Graphics.Blit(texture, renderTex);

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = renderTex;

            Texture2D copyTexture = new Texture2D(renderTex.width, renderTex.height, TextureFormat.ARGB32, true);
            copyTexture.name = texture.name;
            copyTexture.ReadPixels(new Rect(0, 0, renderTex.width, renderTex.height), 0, 0);
            copyTexture.Apply();

            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(renderTex);

            return copyTexture;
        }
    }
}
