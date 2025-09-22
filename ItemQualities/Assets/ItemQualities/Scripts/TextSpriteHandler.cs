using RoR2;
using TMPro;
using UnityEngine;

namespace ItemQualities
{
    static class TextSpriteHandler
    {
        [SystemInitializer]
        static void Init()
        {
            TMP_SpriteAsset itemQualitiesInlineSprites = ItemQualitiesContent.TMP_SpriteAssets.Find("tmpsprInlineSpritesCustom");

            if (itemQualitiesInlineSprites)
            {
                addFallbackSpriteAsset(itemQualitiesInlineSprites);
            }
            else
            {
                Log.Error("Failed to load inline sprites asset");
            }
        }

        static void addFallbackSpriteAsset(TMP_SpriteAsset spriteAsset)
        {
            spriteAsset.material = Material.Instantiate(TMP_Settings.defaultSpriteAsset.material);
            spriteAsset.material.mainTexture = spriteAsset.spriteSheet;

            TMP_Settings.defaultSpriteAsset.fallbackSpriteAssets.Add(spriteAsset);
        }
    }
}
