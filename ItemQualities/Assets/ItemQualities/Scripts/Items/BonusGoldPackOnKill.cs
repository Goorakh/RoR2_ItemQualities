using R2API;
using RoR2;

namespace ItemQualities.Items
{
    static class BonusGoldPackOnKill
    {
        [SystemInitializer]
        static void Init()
        {
            RecalculateStatsAPI.GetStatCoefficients += getStatCoefficients;
        }

        private static void getStatCoefficients(CharacterBody sender, RecalculateStatsAPI.StatHookEventArgs args)
        {
            if (!sender)
                return;

            args.baseDamageAdd += 0.01f * sender.baseDamage * sender.GetBuffCount(ItemQualitiesContent.Buffs.GoldenGun);
        }
    }
}
