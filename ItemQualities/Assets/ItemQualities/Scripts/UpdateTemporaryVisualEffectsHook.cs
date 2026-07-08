using ItemQualities.Utilities.Extensions;
using RoR2;

namespace ItemQualities
{
    internal static class UpdateTemporaryVisualEffectsHook
    {
        [SystemInitializer]
        private static void Init()
        {
            On.RoR2.CharacterBody.UpdateAllTemporaryVisualEffects += CharacterBody_UpdateAllTemporaryVisualEffects;
        }

        private static void CharacterBody_UpdateAllTemporaryVisualEffects(On.RoR2.CharacterBody.orig_UpdateAllTemporaryVisualEffects orig, CharacterBody self)
        {
            orig(self);

            if (self.TryGetComponentCached(out CharacterBodyExtraStatsTracker bodyExtraStats))
            {
                bodyExtraStats.UpdateAllTemporaryVisualEffects();
            }
        }
    }
}
