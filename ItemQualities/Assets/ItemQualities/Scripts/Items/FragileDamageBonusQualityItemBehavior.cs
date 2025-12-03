using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace ItemQualities.Items
{
    public sealed class FragileDamageBonusQualityItemBehavior : MonoBehaviour
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

        CharacterBody _body;
        CharacterBodyExtraStatsTracker _bodyExtraStats;

        bool _buffCountsDirty;

        int _maxHits;

        void Awake()
        {
            _body = GetComponent<CharacterBody>();
            _bodyExtraStats = GetComponent<CharacterBodyExtraStatsTracker>();
        }

        void OnEnable()
        {
            if (NetworkServer.active)
            {
                _body.onInventoryChanged += onInventoryChanged;
                _bodyExtraStats.OnIncomingDamageServer += onIncomingDamageServer;

                onInventoryChanged();
                refreshBuffCounts();
            }
        }

        void Start()
        {
            refreshBuffCounts();
        }

        void OnDisable()
        {
            _body.onInventoryChanged -= onInventoryChanged;
            _bodyExtraStats.OnIncomingDamageServer -= onIncomingDamageServer;

            if (NetworkServer.active)
            {
                ItemQualitiesContent.BuffQualityGroups.FragileDamageBonusBuff.EnsureBuffQualities(_body, QualityTier.None);
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

        void onInventoryChanged()
        {
            ensureBuffQualities();

            QualityTier qualityTier = ItemQualitiesContent.ItemQualityGroups.FragileDamageBonus.GetItemCountsEffective(_body.inventory).HighestQuality;
            switch (qualityTier)
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
            QualityTier buffQualityTier = ItemQualitiesContent.ItemQualityGroups.FragileDamageBonus.GetItemCountsEffective(_body.inventory).HighestQuality;
            ItemQualitiesContent.BuffQualityGroups.FragileDamageBonusBuff.EnsureBuffQualities(_body, buffQualityTier);
        }

        void refreshBuffCounts()
        {
            int hitsTaken = _bodyExtraStats.MasterExtraStatsTracker ? _bodyExtraStats.MasterExtraStatsTracker.StageDamageInstancesTakenCount : 0;

            int currentBuffCount = ItemQualitiesContent.BuffQualityGroups.FragileDamageBonusBuff.GetBuffCounts(_body).TotalQualityCount;
            int targetBuffCount = Mathf.Max(0, _maxHits - hitsTaken);

            int buffCountDiff = targetBuffCount - currentBuffCount;
            if (buffCountDiff != 0)
            {
                ensureBuffQualities();

                QualityTier buffQualityTier = ItemQualitiesContent.ItemQualityGroups.FragileDamageBonus.GetItemCountsEffective(_body.inventory).HighestQuality;
                BuffIndex buffIndex = ItemQualitiesContent.BuffQualityGroups.FragileDamageBonusBuff.GetBuffIndex(buffQualityTier);

                if (buffCountDiff > 0)
                {
                    for (int i = 0; i < buffCountDiff; i++)
                    {
                        _body.AddBuff(buffIndex);
                    }
                }
                else
                {
                    for (int i = 0; i < -buffCountDiff; i++)
                    {
                        _body.RemoveBuff(buffIndex);
                    }
                }

                if (targetBuffCount == 0)
                {
                    if (_watchBreakEffectIndex != EffectIndex.Invalid)
                    {
                        EffectData effectData = new EffectData
                        {
                            origin = _body.corePosition
                        };

                        effectData.SetNetworkedObjectReference(_body.gameObject);

                        EffectManager.SpawnEffect(_watchBreakEffectIndex, effectData, true);
                    }
                }
            }
        }
    }
}
