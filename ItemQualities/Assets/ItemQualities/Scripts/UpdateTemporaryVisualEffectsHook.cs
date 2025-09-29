using RoR2;

namespace ItemQualities
{
    static class UpdateTemporaryVisualEffectsHook
    {
        [SystemInitializer]
        static void Init()
        {
            On.RoR2.CharacterBody.UpdateAllTemporaryVisualEffects += CharacterBody_UpdateAllTemporaryVisualEffects;
        }

        static void CharacterBody_UpdateAllTemporaryVisualEffects(On.RoR2.CharacterBody.orig_UpdateAllTemporaryVisualEffects orig, CharacterBody self)
        {
            orig(self);

            if (self.TryGetComponent(out CharacterBodyExtraStatsTracker bodyExtraStats))
            {
                bodyExtraStats.UpdateAllTemporaryVisualEffects();
            }
        }
    }
}
