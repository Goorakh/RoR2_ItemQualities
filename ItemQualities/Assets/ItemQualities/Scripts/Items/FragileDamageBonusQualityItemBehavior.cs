using ItemQualities.Utilities.Extensions;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace ItemQualities.Items
{
    public sealed class FragileDamageBonusQualityItemBehavior : QualityItemBodyBehavior
    {
        static EffectIndex _watchBreakEffectIndex = EffectIndex.Invalid;

        [SystemInitializer(typeof(EffectCatalogUtils))]
        static void Init()
        {
            _watchBreakEffectIndex = EffectCatalogUtils.FindEffectIndex("DelicateWatchProcEffect");
            if (_watchBreakEffectIndex == EffectIndex.Invalid)
            {
                Log.Error("Failed to find watch break effect index");
            }
        }

        [ItemGroupAssociation(QualityItemBehaviorUsageFlags.Server)]
        static ItemQualityGroup GetItemGroup()
        {
            return ItemQualitiesContent.ItemQualityGroups.FragileDamageBonus;
        }

        CharacterBodyExtraStatsTracker _bodyExtraStats;

        bool _buffCountsDirty;

        int _maxHits;

        protected override void Awake()
        {
            base.Awake();
            _bodyExtraStats = this.GetComponentCached<CharacterBodyExtraStatsTracker>();
        }

        void Start()
        {
            refreshBuffCounts();
        }

        void OnEnable()
        {
            _bodyExtraStats.OnIncomingDamageServer += onIncomingDamageServer;

            refreshBuffCounts();
        }

        void OnDisable()
        {
            _bodyExtraStats.OnIncomingDamageServer -= onIncomingDamageServer;

            if (NetworkServer.active)
            {
                Body.RemoveAllQualityBuffs(ItemQualitiesContent.BuffQualityGroups.FragileDamageBonusBuff);
            }
        }

        void FixedUpdate()
        {
            if (_buffCountsDirty)
            {
                _buffCountsDirty = false;
                refreshBuffCounts();
            }
        }

        void onIncomingDamageServer(DamageInfo damageInfo)
        {
            if (damageInfo.damage > 0f && !damageInfo.delayedDamageSecondHalf)
            {
                _buffCountsDirty = true;
            }
        }

        protected override void OnStacksChanged()
        {
            base.OnStacksChanged();

            ensureBuffQualities();

            switch (Stacks.HighestQuality)
            {
                case QualityTier.Uncommon:
                    _maxHits = 10;
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

        void ensureBuffQualities()
        {
            Body.ConvertQualityBuffsToTier(ItemQualitiesContent.BuffQualityGroups.FragileDamageBonusBuff, Stacks.HighestQuality);
        }

        void refreshBuffCounts()
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
