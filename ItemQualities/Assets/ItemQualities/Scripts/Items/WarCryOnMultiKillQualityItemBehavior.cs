using RoR2;
using UnityEngine;

namespace ItemQualities.Items
{
    public sealed class WarCryOnMultiKillQualityItemBehavior : MonoBehaviour
    {
        CharacterBody _body;
        CharacterBodyExtraStatsTracker _bodyExtraStats;

        bool _hadWarCryBuff;

        bool hasWarCryBuff => _body.HasBuff(RoR2Content.Buffs.WarCryBuff) || _body.HasBuff(RoR2Content.Buffs.TeamWarCry);

        void Awake()
        {
            _body = GetComponent<CharacterBody>();
            _bodyExtraStats = GetComponent<CharacterBodyExtraStatsTracker>();
        }

        void OnEnable()
        {
            _body.onInventoryChanged += onInventoryChanged;

            _bodyExtraStats.OnKilledOther += onKilledOther;

            _hadWarCryBuff = false;
        }

        void OnDisable()
        {
            _body.onInventoryChanged -= onInventoryChanged;

            _bodyExtraStats.OnKilledOther -= onKilledOther;

            setWarCryBuffCount(0);
        }

        void FixedUpdate()
        {
            bool hasBuff = hasWarCryBuff;
            if (hasBuff != _hadWarCryBuff)
            {
                setWarCryBuffCount(hasBuff ? _bodyExtraStats.EliteKillCount : 0);
                _hadWarCryBuff = hasBuff;
            }
        }

        void onKilledOther(DamageReport damageReport)
        {
            if (damageReport.victimIsElite)
            {
                if (hasWarCryBuff)
                {
                    setWarCryBuffCount(_bodyExtraStats.EliteKillCount);
                }
            }
        }

        void onInventoryChanged()
        {
            setWarCryBuffCount(hasWarCryBuff ? _bodyExtraStats.EliteKillCount : 0);
        }

        void setWarCryBuffCount(int count)
        {
            int currentBuffCount = ItemQualitiesContent.BuffQualityGroups.MultikillWarCryBuff.GetBuffCounts(_body).TotalQualityCount;
            if (currentBuffCount != count)
            {
                QualityTier qualityTier = ItemQualitiesContent.ItemQualityGroups.WarCryOnMultiKill.GetItemCountsEffective(_body.inventory).HighestQuality;
                BuffIndex qualityBuffIndex = ItemQualitiesContent.BuffQualityGroups.MultikillWarCryBuff.GetBuffIndex(qualityTier);

                if (currentBuffCount < count)
                {
                    for (int i = currentBuffCount; i < count; i++)
                    {
                        _body.AddBuff(qualityBuffIndex);
                    }
                }
                else
                {
                    for (int i = currentBuffCount; i > count; i--)
                    {
                        _body.RemoveBuff(qualityBuffIndex);
                    }
                }

                updateBuffQualities();
            }
        }

        void updateBuffQualities()
        {
            QualityTier qualityTier = ItemQualitiesContent.ItemQualityGroups.WarCryOnMultiKill.GetItemCountsEffective(_body.inventory).HighestQuality;
            ItemQualitiesContent.BuffQualityGroups.MultikillWarCryBuff.EnsureBuffQualities(_body, qualityTier);
        }
    }
}
