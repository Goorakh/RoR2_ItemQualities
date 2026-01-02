using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace ItemQualities.Items
{
    public sealed class BonusGoldPackOnKillQualityItemBehavior : QualityItemBodyBehavior
    {
        [ItemGroupAssociation(QualityItemBehaviorUsageFlags.Server)]
        static ItemQualityGroup GetItemGroup()
        {
            return ItemQualitiesContent.ItemQualityGroups.BonusGoldPackOnKill;
        }

        uint _lastGoldAmount;

        float _buffRefreshTimer;

        void OnEnable()
        {
            refreshBuff();
            _lastGoldAmount = Body.master ? Body.master.money : 0;
        }

        void OnDisable()
        {
            if (NetworkServer.active)
            {
                for (int i = Body.GetBuffCount(ItemQualitiesContent.Buffs.GoldenGun); i > 0; i--)
                {
                    Body.RemoveBuff(ItemQualitiesContent.Buffs.GoldenGun);
                }
            }
        }

        void FixedUpdate()
        {
            _buffRefreshTimer -= Time.fixedDeltaTime;
            if (_buffRefreshTimer <= 0f)
            {
                _buffRefreshTimer = 0.2f;

                uint goldAmount = Body.master ? Body.master.money : 0;
                if (goldAmount != _lastGoldAmount)
                {
                    _lastGoldAmount = goldAmount;
                    refreshBuff();
                }
            }
        }

        void refreshBuff()
        {
            ItemQualityCounts bonusGoldPackOnKill = Stacks;

            int maxBuffCount = (20 * bonusGoldPackOnKill.UncommonCount) +
                               (40 * bonusGoldPackOnKill.RareCount) +
                               (60 * bonusGoldPackOnKill.EpicCount) +
                               (100 * bonusGoldPackOnKill.LegendaryCount);

            float targetBuffCountMultiplier;
            switch (bonusGoldPackOnKill.HighestQuality)
            {
                case QualityTier.None:
                    targetBuffCountMultiplier = 0f;
                    break;
                case QualityTier.Uncommon:
                    targetBuffCountMultiplier = 2f;
                    break;
                case QualityTier.Rare:
                    targetBuffCountMultiplier = 3f;
                    break;
                case QualityTier.Epic:
                    targetBuffCountMultiplier = 3.5f;
                    break;
                case QualityTier.Legendary:
                    targetBuffCountMultiplier = 4f;
                    break;
                default:
                    targetBuffCountMultiplier = 0f;
                    Log.Error($"Quality tier {bonusGoldPackOnKill.HighestQuality} is not implemented");
                    break;
            }

            uint money = Body.master ? Body.master.money : 0;
            int moneyAmountPerBuff = Run.instance.GetDifficultyScaledCost(25, Stage.instance.entryDifficultyCoefficient);

            int desiredBuffCount = (int)(targetBuffCountMultiplier * (money / (float)moneyAmountPerBuff));
            int targetBuffCount = Mathf.Min(maxBuffCount, desiredBuffCount);

            int currentBuffCount = Body.GetBuffCount(ItemQualitiesContent.Buffs.GoldenGun);

            if (targetBuffCount != currentBuffCount)
            {
                if (targetBuffCount > currentBuffCount)
                {
                    for (int i = currentBuffCount; i < targetBuffCount; i++)
                    {
                        Body.AddBuff(ItemQualitiesContent.Buffs.GoldenGun);
                    }
                }
                else // targetBuffCount < currentBuffCount
                {
                    for (int i = currentBuffCount; i > targetBuffCount; i--)
                    {
                        Body.RemoveBuff(ItemQualitiesContent.Buffs.GoldenGun);
                    }
                }
            }
        }
    }
}
