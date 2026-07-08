using ItemQualities.Utilities.Extensions;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace ItemQualities.Items
{
    public sealed class FragileDamageBonusQualityItemBehavior : QualityItemBodyBehavior
    {
        private static EffectIndex _watchBreakEffectIndex = EffectIndex.Invalid;

        [SystemInitializer(typeof(EffectCatalogUtils))]
        private static void Init()
        {
            _watchBreakEffectIndex = EffectCatalogUtils.FindEffectIndex("DelicateWatchProcEffect");
            if (_watchBreakEffectIndex == EffectIndex.Invalid)
            {
                Log.Error("Failed to find watch break effect index");
            }
        }

        [ItemGroupAssociation(QualityItemBehaviorUsageFlags.Server)]
        private static ItemQualityGroup GetItemGroup()
        {
            return ItemQualitiesContent.ItemQualityGroups.FragileDamageBonus;
        }

        private CharacterBodyExtraStatsTracker _bodyExtraStats;

        private bool _buffCountsDirty;

        private int _maxHits;

        protected override void Awake()
        {
            base.Awake();
            _bodyExtraStats = this.GetComponentCached<CharacterBodyExtraStatsTracker>();
        }

        private void OnEnable()
        {
            if (_bodyExtraStats.MasterExtraStatsTracker)
            {
                _bodyExtraStats.MasterExtraStatsTracker.OnStageDamageInstancesTakenCountChangedServer += onStageDamageInstancesTakenCountChangedServer;
            }

            refreshBuffCounts();
        }

        private void OnDisable()
        {
            if (_bodyExtraStats.MasterExtraStatsTracker)
            {
                _bodyExtraStats.MasterExtraStatsTracker.OnStageDamageInstancesTakenCountChangedServer -= onStageDamageInstancesTakenCountChangedServer;
            }

            if (NetworkServer.active)
            {
                Body.RemoveAllQualityBuffs(ItemQualitiesContent.BuffQualityGroups.FragileDamageBonusBuff);
            }
        }

        private void FixedUpdate()
        {
            if (_buffCountsDirty)
            {
                _buffCountsDirty = false;
                refreshBuffCounts();
            }
        }

        private void onStageDamageInstancesTakenCountChangedServer(CharacterMasterExtraStatsTracker _)
        {
            _buffCountsDirty = true;
        }

        protected override void OnStacksChanged()
        {
            base.OnStacksChanged();

            ensureBuffQualities();

            switch (Stacks.HighestQuality)
            {
                case QualityTier.Uncommon:
                    _maxHits = 12;
                    break;
                case QualityTier.Rare:
                    _maxHits = 15;
                    break;
                case QualityTier.Epic:
                    _maxHits = 20;
                    break;
                case QualityTier.Legendary:
                    _maxHits = 25;
                    break;
                default:
                    _maxHits = 0;
                    break;
            }

            refreshBuffCounts();
        }

        private void ensureBuffQualities()
        {
            Body.ConvertQualityBuffsToTier(ItemQualitiesContent.BuffQualityGroups.FragileDamageBonusBuff, Stacks.HighestQuality);
        }

        private void refreshBuffCounts()
        {
            int hitsTaken = _bodyExtraStats.MasterExtraStatsTracker ? _bodyExtraStats.MasterExtraStatsTracker.StageDamageInstancesTakenCount : 0;

            int currentBuffCount = Body.GetBuffCounts(ItemQualitiesContent.BuffQualityGroups.FragileDamageBonusBuff).TotalQualityCount;
            int targetBuffCount = Mathf.Max(0, _maxHits - hitsTaken);

            int buffCountDiff = targetBuffCount - currentBuffCount;
            if (buffCountDiff != 0)
            {
                ensureBuffQualities();

                BuffIndex buffIndex = ItemQualitiesContent.BuffQualityGroups.FragileDamageBonusBuff.GetBuffIndex(Stacks.HighestQuality);

                if (buffCountDiff > 0)
                {
                    for (int i = 0; i < buffCountDiff; i++)
                    {
                        Body.AddBuff(buffIndex);
                    }
                }
                else
                {
                    for (int i = 0; i < -buffCountDiff; i++)
                    {
                        Body.RemoveBuff(buffIndex);
                    }
                }

                if (targetBuffCount == 0)
                {
                    if (_watchBreakEffectIndex != EffectIndex.Invalid)
                    {
                        EffectData effectData = new EffectData
                        {
                            origin = Body.corePosition
                        };

                        effectData.SetNetworkedObjectReference(Body.gameObject);

                        EffectManager.SpawnEffect(_watchBreakEffectIndex, effectData, true);
                    }
                }
            }
        }
    }
}
