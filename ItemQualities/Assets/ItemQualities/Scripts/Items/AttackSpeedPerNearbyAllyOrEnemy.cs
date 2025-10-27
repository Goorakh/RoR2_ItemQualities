using R2API;
using RoR2;
using System;

namespace ItemQualities.Items
{
    static class AttackSpeedPerNearbyAllyOrEnemy
    {
        [SystemInitializer]
        static void Init()
        {
            IL.RoR2.AttackSpeedPerNearbyCollider.ServerUpdateValuesFromInventory += ItemHooks.CombineGroupedItemCountsPatch;
            IL.RoR2.AttackSpeedPerNearbyCollider.Start += ItemHooks.CombineGroupedItemCountsPatch;

            On.RoR2.AttackSpeedPerNearbyCollider.UpdateValues += AttackSpeedPerNearbyCollider_UpdateValues;

            On.RoR2.AttackSpeedPerNearbyCollider.SetIndicatorDiameter += AttackSpeedPerNearbyCollider_SetIndicatorDiameter;

            RecalculateStatsAPI.GetStatCoefficients += RecalculateStatsAPI_GetStatCoefficients;
        }

        static void RecalculateStatsAPI_GetStatCoefficients(CharacterBody sender, RecalculateStatsAPI.StatHookEventArgs args)
        {
            BuffQualityCounts attackSpeedPerNearbyAllyOrEnemyBuff = ItemQualitiesContent.BuffQualityGroups.AttackSpeedPerNearbyAllyOrEnemyBuff.GetBuffCounts(sender);
            if (attackSpeedPerNearbyAllyOrEnemyBuff.TotalQualityCount > 0)
            {
                ItemQualityCounts attackSpeedPerNearbyAllyOrEnemy = ItemQualitiesContent.ItemQualityGroups.AttackSpeedPerNearbyAllyOrEnemy.GetItemCounts(sender.inventory);
                QualityTier qualityTier = attackSpeedPerNearbyAllyOrEnemy.HighestQuality;

                float attackSpeedPerBuff = 0f;
                switch (qualityTier)
                {
                    case QualityTier.None:
                    case QualityTier.Uncommon:
                        attackSpeedPerBuff = 0.05f;
                        break;
                    case QualityTier.Rare:
                        attackSpeedPerBuff = 0.10f;
                        break;
                    case QualityTier.Epic:
                        attackSpeedPerBuff = 0.20f;
                        break;
                    case QualityTier.Legendary:
                        attackSpeedPerBuff = 0.35f;
                        break;
                    default:
                        Log.Error($"Quality tier {qualityTier} is not implemented");
                        break;
                }

                if (attackSpeedPerNearbyAllyOrEnemy.UncommonCount > 0)
                {
                    attackSpeedPerBuff += 0.025f * (attackSpeedPerNearbyAllyOrEnemy.UncommonCount - 1);
                }

                if (attackSpeedPerNearbyAllyOrEnemy.RareCount > 0)
                {
                    attackSpeedPerBuff += 0.05f * (attackSpeedPerNearbyAllyOrEnemy.RareCount - 1);
                }

                if (attackSpeedPerNearbyAllyOrEnemy.EpicCount > 0)
                {
                    attackSpeedPerBuff += 0.1f * (attackSpeedPerNearbyAllyOrEnemy.EpicCount - 1);
                }

                if (attackSpeedPerNearbyAllyOrEnemy.LegendaryCount > 0)
                {
                    attackSpeedPerBuff += 0.15f * (attackSpeedPerNearbyAllyOrEnemy.LegendaryCount - 1);
                }

                args.attackSpeedMultAdd += attackSpeedPerBuff * attackSpeedPerNearbyAllyOrEnemyBuff.TotalQualityCount;
            }
        }

        static void AttackSpeedPerNearbyCollider_UpdateValues(On.RoR2.AttackSpeedPerNearbyCollider.orig_UpdateValues orig, AttackSpeedPerNearbyCollider self, int itemCount, out float diameter)
        {
            orig(self, itemCount, out diameter);

            if (!self.body)
                return;

            BuffQualityCounts attackSpeedPerNearbyAllyOrEnemyBuff = ItemQualitiesContent.BuffQualityGroups.AttackSpeedPerNearbyAllyOrEnemyBuff.GetBuffCounts(self.body);
            if (attackSpeedPerNearbyAllyOrEnemyBuff.TotalQualityCount > 0)
            {
                ItemQualityCounts attackSpeedPerNearbyAllyOrEnemy = ItemQualitiesContent.ItemQualityGroups.AttackSpeedPerNearbyAllyOrEnemy.GetItemCounts(self.body.inventory);
                QualityTier qualityTier = attackSpeedPerNearbyAllyOrEnemy.HighestQuality;

                float diameterPerBuff = 0f;
                switch (qualityTier)
                {
                    case QualityTier.None:
                    case QualityTier.Uncommon:
                        diameterPerBuff = 5f;
                        break;
                    case QualityTier.Rare:
                        diameterPerBuff = 10f;
                        break;
                    case QualityTier.Epic:
                        diameterPerBuff = 20f;
                        break;
                    case QualityTier.Legendary:
                        diameterPerBuff = 40f;
                        break;
                    default:
                        Log.Error($"Quality tier {qualityTier} is not implemented");
                        break;
                }

                if (attackSpeedPerNearbyAllyOrEnemy.UncommonCount > 0)
                {
                    diameterPerBuff += 2f * (attackSpeedPerNearbyAllyOrEnemy.UncommonCount - 1);
                }

                if (attackSpeedPerNearbyAllyOrEnemy.RareCount > 0)
                {
                    diameterPerBuff += 5f * (attackSpeedPerNearbyAllyOrEnemy.RareCount - 1);
                }

                if (attackSpeedPerNearbyAllyOrEnemy.EpicCount > 0)
                {
                    diameterPerBuff += 10f * (attackSpeedPerNearbyAllyOrEnemy.EpicCount - 1);
                }

                if (attackSpeedPerNearbyAllyOrEnemy.LegendaryCount > 0)
                {
                    diameterPerBuff += 20f * (attackSpeedPerNearbyAllyOrEnemy.LegendaryCount - 1);
                }

                diameter += diameterPerBuff * attackSpeedPerNearbyAllyOrEnemyBuff.TotalQualityCount;
            }
        }

        static void AttackSpeedPerNearbyCollider_SetIndicatorDiameter(On.RoR2.AttackSpeedPerNearbyCollider.orig_SetIndicatorDiameter orig, AttackSpeedPerNearbyCollider self, float diameter)
        {
            if (self.TryGetComponent(out AttackSpeedPerNearbyColliderQualityController qualityController) && !qualityController.HandleSetDiameter(diameter))
                return;

            orig(self, diameter);
        }
    }
}
