using ItemQualities.Utilities.Extensions;
using R2API;
using RoR2;

namespace ItemQualities.Buffs
{
    static class TeamWarCry
    {
        [SystemInitializer]
        static void Init()
        {
            RecalculateStatsAPI.GetStatCoefficients += getStatCoefficients;

            BuffHooks.OnBuffFirstStackGainedGlobal += onBuffFirstStackGainedGlobal;
        }

        static void getStatCoefficients(CharacterBody sender, RecalculateStatsAPI.StatHookEventArgs args)
        {
            BuffQualityCounts teamWarCry = sender.GetBuffCounts(ItemQualitiesContent.BuffQualityGroups.TeamWarCry);
            QualityTier qualityTier = teamWarCry.HighestQuality;
            if (qualityTier == QualityTier.None)
                return;

            float cooldownReduction;
            switch (qualityTier)
            {
                case QualityTier.Uncommon:
                    cooldownReduction = 2f;
                    break;
                case QualityTier.Rare:
                    cooldownReduction = 5f;
                    break;
                case QualityTier.Epic:
                    cooldownReduction = 8f;
                    break;
                case QualityTier.Legendary:
                    cooldownReduction = 15f;
                    break;
                default:
                    cooldownReduction = 0f;
                    Log.Warning($"Quality tier {qualityTier} is not implemented");
                    break;
            }

            if (cooldownReduction > 0)
            {
                args.allSkills.cooldownFlatReduction += cooldownReduction;
            }
        }

        static void onBuffFirstStackGainedGlobal(CharacterBody body, BuffDef buffDef)
        {
            BuffIndex buffIndex = buffDef ? buffDef.buffIndex : BuffIndex.None;

            if (QualityCatalog.GetQualityTier(buffIndex) == QualityTier.None)
                return;

            if (QualityCatalog.FindBuffQualityGroupIndex(buffIndex) != ItemQualitiesContent.BuffQualityGroups.TeamWarCry.GroupIndex)
                return;

            foreach (GenericSkill skill in body.skillLocator.AllSkills)
            {
                if (skill.stock < skill.maxStock)
                {
                    skill.AddOneStock();
                    body.OnSkillCooldown(skill);
                }
            }
        }
    }
}
