using RoR2;
using TMPro;

namespace ItemQualities
{
    static class TextSpriteHandler
    {
        [SystemInitializer]
        static void Init()
        {
            foreach (TMP_SpriteAsset spriteAsset in ItemQualitiesContent.TMP_SpriteAssets.AllSpriteAssets)
            {
                registerSpriteAsset(spriteAsset);
            }
        }

        static void registerSpriteAsset(TMP_SpriteAsset spriteAsset)
        {
            TMP_Settings.defaultSpriteAsset.fallbackSpriteAssets.Add(spriteAsset);
        }
    }
}
