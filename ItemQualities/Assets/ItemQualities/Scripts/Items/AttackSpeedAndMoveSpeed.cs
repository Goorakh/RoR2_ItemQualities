using ItemQualities.Utilities.Extensions;
using R2API;
using RoR2;

namespace ItemQualities.Items
{
    static class AttackSpeedAndMoveSpeed
    {
        // This is a bit strange, but the other approach would be to IL hook RecalculateStats, collect all the locals, and re-assign move and attack speed at the end, which would have compatibility issues, and also be a nightmare to maintain. Since recalcstats is in theory deterministic and not dependent on any external state (except now it is lol), calling it twice like this *should* be fine

        static BonusType _currentCallBonusType = BonusType.None;

        [SystemInitializer]
        static void Init()
        {
            On.RoR2.CharacterBody.RecalculateStats += CharacterBody_RecalculateStats;
            RecalculateStatsAPI.GetStatCoefficients += RecalculateStatsAPI_GetStatCoefficients;
        }

        static void RecalculateStatsAPI_GetStatCoefficients(CharacterBody sender, RecalculateStatsAPI.StatHookEventArgs args)
        {
            Inventory inventory = sender ? sender.inventory : null;
            if (!inventory)
                return;

            ItemQualityCounts attackSpeedAndMoveSpeed = inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.AttackSpeedAndMoveSpeed);

            switch (_currentCallBonusType)
            {
                case BonusType.AttackSpeed:
                    args.attackSpeedMultAdd += (0.10f * attackSpeedAndMoveSpeed.UncommonCount) +
                                               (0.20f * attackSpeedAndMoveSpeed.RareCount) +
                                               (0.40f * attackSpeedAndMoveSpeed.EpicCount) +
                                               (0.60f * attackSpeedAndMoveSpeed.LegendaryCount);
                    break;
                case BonusType.MoveSpeed:
                    args.moveSpeedMultAdd += (0.10f * attackSpeedAndMoveSpeed.UncommonCount) +
                                             (0.20f * attackSpeedAndMoveSpeed.RareCount) +
                                             (0.40f * attackSpeedAndMoveSpeed.EpicCount) +
                                             (0.60f * attackSpeedAndMoveSpeed.LegendaryCount);
                    break;
            }
        }

        static void CharacterBody_RecalculateStats(On.RoR2.CharacterBody.orig_RecalculateStats orig, CharacterBody self)
        {
            orig(self);

            if (_currentCallBonusType != BonusType.None)
                return;

            ItemQualityCounts attackSpeedAndMoveSpeed = default;
            if (self && self.inventory)
            {
                attackSpeedAndMoveSpeed = self.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.AttackSpeedAndMoveSpeed);
            }

            if (attackSpeedAndMoveSpeed.TotalQualityCount > 0)
            {
                BonusType bonusType = BonusType.None;

                float nonSprintSpeed = self.moveSpeed;
                if (self.isSprinting)
                    nonSprintSpeed /= self.sprintingSpeedMultiplier;

                float moveSpeedPercent = self.baseMoveSpeed > 0 ? nonSprintSpeed / self.baseMoveSpeed : 1f;
                float attackSpeedPercent = self.baseAttackSpeed > 0 ? self.attackSpeed / self.baseAttackSpeed : 1f;

                if (attackSpeedPercent < moveSpeedPercent)
                {
                    bonusType = BonusType.AttackSpeed;
                }
                else // Bias towards movespeed, otherwise there can be situations where the item does nothing
                {
                    bonusType = BonusType.MoveSpeed;
                }

                if (bonusType != BonusType.None)
                {
                    _currentCallBonusType = bonusType;
                    try
                    {
                        self.RecalculateStats();
                    }
                    finally
                    {
                        _currentCallBonusType = BonusType.None;
                    }
                }
            }
        }

        enum BonusType
        {
            None,
            AttackSpeed,
            MoveSpeed
        }
    }
}
