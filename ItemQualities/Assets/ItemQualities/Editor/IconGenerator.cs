using System.IO;
using UnityEditor;
using UnityEngine;

namespace ItemQualities.Editor
{
    public static class IconGenerator
    {
        [MenuItem("Tools/ItemQualities/Generate Quality Icons")]
        public static void GenerateQualityIcons()
        {
            GenerateQualityIcons(false);
        }

        [MenuItem("Tools/ItemQualities/Generate Quality Icons (Consumed)")]
        public static void GenerateQualityIconsConsumed()
        {
            GenerateQualityIcons(true);
        }

        public static void GenerateQualityIcons(bool useConsumedIcon)
        {
            string selectedAssetGuid = Selection.assetGUIDs[0];
            Texture2D selectedTexture = (Texture2D)Selection.objects[0];

            string destinationDirectory = Path.GetDirectoryName(AssetDatabase.GetAssetPath(selectedTexture));

            string textureName = selectedTexture.name;
            if (textureName.StartsWith("tex"))
                textureName = textureName.Substring(3);

            TextureImporter selectedTextureImporter = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(selectedAssetGuid));

            for (QualityTier qualityTier = 0; qualityTier < QualityTier.Count; qualityTier++)
            {
                string qualityIconTextureName = $"{textureName}{qualityTier}";
                if (useConsumedIcon)
                {
                    qualityIconTextureName += "Consumed";
                }

                string qualityIconTextureAssetPath = Path.Combine(destinationDirectory, $"tex{qualityIconTextureName}.png");

                Texture2D qualityIconTexture = QualityCatalog.CreateQualityIconTexture(selectedTexture, qualityTier, useConsumedIcon);

                File.WriteAllBytes(qualityIconTextureAssetPath, qualityIconTexture.EncodeToPNG());

                AssetDatabase.ImportAsset(qualityIconTextureAssetPath);

                TextureImporter textureImporter = (TextureImporter)AssetImporter.GetAtPath(qualityIconTextureAssetPath);
                textureImporter.textureType = TextureImporterType.Sprite;
                textureImporter.spritePixelsPerUnit = selectedTextureImporter.spritePixelsPerUnit;
                textureImporter.alphaIsTransparency = selectedTextureImporter.alphaIsTransparency;
                textureImporter.SaveAndReimport();
            }
        }

        [MenuItem("Tools/ItemQualities/Generate Quality Icons", true)]
        [MenuItem("Tools/ItemQualities/Generate Quality Icons (Consumed)", true)]
        public static bool ValidateGenerateQualityIcons()
        {
            return Selection.count == 1 && Selection.objects[0] is Texture2D;
        }
    }
}