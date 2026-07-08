using RoR2;
using TMPro;

namespace ItemQualities
{
    internal static class TextSpriteHandler
    {
        [SystemInitializer]
        private static void Init()
        {
            foreach (TMP_SpriteAsset spriteAsset in ItemQualitiesContent.TMP_SpriteAssets.AllSpriteAssets)
            {
                registerSpriteAsset(spriteAsset);
            }
        }

        private static void registerSpriteAsset(TMP_SpriteAsset spriteAsset)
        {
            TMP_Settings.defaultSpriteAsset.fallbackSpriteAssets.Add(spriteAsset);
        }
    }
}
