using HG;
using RoR2;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace ItemQualities.Utilities
{
    internal static class BodyPortraitIconSpriteCache
    {
        private static Sprite[] _iconSprites = Array.Empty<Sprite>();

        [SystemInitializer(typeof(BodyCatalog))]
        private static void Init()
        {
            _iconSprites = new Sprite[BodyCatalog.bodyCount];

            Dictionary<Texture2D, Sprite> spriteCache = new Dictionary<Texture2D, Sprite>(BodyCatalog.bodyCount);

            foreach (CharacterBody body in BodyCatalog.allBodyPrefabBodyBodyComponents)
            {
                if (!body.portraitIcon || body.portraitIcon is not Texture2D portraitIcon)
                {
                    continue;
                }

                if (!spriteCache.TryGetValue(portraitIcon, out Sprite portraitSprite))
                {
                    portraitSprite = Sprite.Create(portraitIcon, new Rect(Vector2.zero, new Vector2(portraitIcon.width, portraitIcon.height)), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, Vector4.zero, false);
                    portraitSprite.name = portraitIcon.name;

                    spriteCache.Add(portraitIcon, portraitSprite);
                }

                _iconSprites[(int)body.bodyIndex] = portraitSprite;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Sprite GetBodyIconSprite(BodyIndex bodyIndex)
        {
            return ArrayUtils.GetSafe(_iconSprites, (int)bodyIndex);
        }
    }
}
