using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ItemQualities
{
    [CreateAssetMenu(menuName = "ItemQualities/ColorRampTextureGenerator")]
    public sealed class ColorRampDef : ScriptableObject
    {
        [Tooltip("The width of the generated texture")]
        [Min(1)]
        public int TextureWidth = 256;

        [Tooltip("The height of each gradient segment")]
        [Min(1)]
        public int SegmentHeight = 16;

        public Segment[] Segments = Array.Empty<Segment>();

#if UNITY_EDITOR
        [ContextMenu("Generate Texture")]
        public void GenerateTexture()
        {
            string currentDirectory = Path.GetDirectoryName(AssetDatabase.GetAssetPath(this));
            string textureAssetPath = Path.Combine(currentDirectory, "tex" + name + ".png");

            int textureWidth = TextureWidth;
            int textureHeight = Segments.Length * SegmentHeight;

            Texture2D texture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, 0, true);

            for (int segmentIdx = 0; segmentIdx < Segments.Length; segmentIdx++)
            {
                for (int x = 0; x < textureWidth; x++)
                {
                    Color color = Segments[segmentIdx].Gradient.Evaluate(x / (float)(textureWidth - 1));

                    for (int segmentY = 0; segmentY < SegmentHeight; segmentY++)
                    {
                        int y = textureHeight - ((SegmentHeight * segmentIdx) + segmentY + 1);
                        texture.SetPixel(x, y, color);
                    }
                }
            }

            texture.Apply();

            File.WriteAllBytes(textureAssetPath, texture.EncodeToPNG());
            Debug.Log($"Created ramp texture '{textureAssetPath}'");
        }
#endif

        [Serializable]
        public struct Segment
        {
            public string Name;

            public Gradient Gradient;
        }
    }
}
