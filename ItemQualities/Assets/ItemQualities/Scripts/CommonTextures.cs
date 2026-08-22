using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using RoR2;
using RoR2BepInExPack.GameAssetPathsBetter;
using UnityEngine;

namespace ItemQualities
{
    internal static class CommonTextures
    {
        public static Texture2D DefaultElitesRamp { get; private set; }

        [InitDuringStartupPhase(GameInitPhase.DuringIntro)]
        private static void Init()
        {
            AddressableUtil.LoadAssetAsync<Texture2D>(RoR2_Base_Common_GlobalTextures.texRampElites_psd).OnSuccess(rampElites =>
            {
                DefaultElitesRamp = rampElites;
            });
        }
    }
}
