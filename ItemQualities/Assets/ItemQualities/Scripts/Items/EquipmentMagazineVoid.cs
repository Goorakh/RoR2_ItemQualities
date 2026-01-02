using ItemQualities.Utilities.Extensions;
using R2API;
using RoR2;

namespace ItemQualities.Items
{
    static class EquipmentMagazineVoid
    {
        [SystemInitializer]
        static void Init()
        {
            ItemHooks.TakeDamageModifier += modifyTakeDamage;

            RecalculateStatsAPI.GetStatCoefficients += getStatCoefficients;
        }

        static void modifyTakeDamage(ref float damageValue, DamageInfo damageInfo)
        {
            if (damageInfo == null || (damageInfo.damageType.damageSource & DamageSource.Special) == 0)
                return;

            CharacterBody attackerBody = damageInfo.attacker ? damageInfo.attacker.GetComponent<CharacterBody>() : null;
            Inventory attackerInventory = attackerBody ? attackerBody.inventory : null;
            if (!attackerInventory)
                return;

            ItemQualityCounts equipmentMagazineVoid = attackerInventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.EquipmentMagazineVoid);
            if (equipmentMagazineVoid.TotalQualityCount > 0)
            {
                float damageIncrease = (0.1f * equipmentMagazineVoid.UncommonCount) +
                                       (0.2f * equipmentMagazineVoid.RareCount) +
                                       (0.4f * equipmentMagazineVoid.EpicCount) +
                                       (0.5f * equipmentMagazineVoid.LegendaryCount);

                if (damageIncrease > 0f)
                {
                    damageValue *= 1f + damageIncrease;
                    damageInfo.damageColorIndex = DamageColorIndex.Void;
                }
            }
        }

        static void getStatCoefficients(CharacterBody sender, RecalculateStatsAPI.StatHookEventArgs args)
        {
            if (!sender || !sender.inventory)
                return;

            ItemQualityCounts equipmentMagazineVoid = sender.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.EquipmentMagazineVoid);
            if (equipmentMagazineVoid.TotalQualityCount > 0)
            {
                float specialSkillCooldownScale;
                switch (equipmentMagazineVoid.HighestQuality)
                {
                    case QualityTier.Uncommon:
                        specialSkillCooldownScale = 1f - 0.1f;
                        break;
                    case QualityTier.Rare:
                        specialSkillCooldownScale = 1f - 0.2f;
                        break;
                    case QualityTier.Epic:
                        specialSkillCooldownScale = 1f - 0.4f;
                        break;
                    case QualityTier.Legendary:
                        specialSkillCooldownScale = 1f - 0.55f;
                        break;
                    default:
                        specialSkillCooldownScale = 1f;
                        Log.Error($"Quality tier {equipmentMagazineVoid.HighestQuality} is not implemented");
                        break;
                }

                args.specialSkill.cooldownMultiplier *= specialSkillCooldownScale;
            }
        }
    }
}
