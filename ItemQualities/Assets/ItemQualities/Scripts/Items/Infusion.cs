using ItemQualities.Utilities.Extensions;
using R2API;
using RoR2;
using RoR2.Orbs;
using UnityEngine;
using UnityEngine.Networking;

namespace ItemQualities.Items
{
    internal static class Infusion
    {
        [SystemInitializer]
        private static void Init()
        {
            RecalculateStatsAPI.GetStatCoefficients += getStatCoefficients;

            GlobalEventManager.onCharacterDeathGlobal += onCharacterDeathGlobal;
        }

        private static void getStatCoefficients(CharacterBody sender, RecalculateStatsAPI.StatHookEventArgs args)
        {
            if (!sender.inventory)
            {
                return;
            }

            ItemQualityCounts infusion = sender.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.Infusion);
            if (infusion.TotalQualityCount > 0)
            {
                CharacterMaster master = sender.master;
                if (master && master.TryGetComponentCached(out CharacterMasterExtraStatsTracker masterStats))
                {
                    args.baseHealthAdd += masterStats.QualityInfusionBonus;
                }
            }
        }

        private static void onCharacterDeathGlobal(DamageReport damageReport)
        {
            if (!NetworkServer.active)
                return;

            if (damageReport?.damageInfo == null)
                return;

            if (damageReport.attackerBody && damageReport.attackerMaster && damageReport.attackerMaster.inventory)
            {
                if (damageReport.victimIsBoss || damageReport.victimIsChampion)
                {
                    Vector3 victimPosition = damageReport.damageInfo.position;
                    if (damageReport.victimBody)
                    {
                        victimPosition = damageReport.victimBody.corePosition;
                    }

                    ItemQualityCounts infusion = damageReport.attackerMaster.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.Infusion);

                    if (infusion.TotalQualityCount > 0)
                    {
                        uint infusionBonus = (10 * (uint)infusion.UncommonCount) +
                                             (20 * (uint)infusion.RareCount) +
                                             (35 * (uint)infusion.EpicCount) +
                                             (55 * (uint)infusion.LegendaryCount);

                        QualityInfusionOrb infusionOrb = new QualityInfusionOrb
                        {
                            origin = victimPosition,
                            target = damageReport.attackerBody.mainHurtBox,
                            maxHpValue = infusionBonus
                        };

                        OrbManager.instance.AddOrb(infusionOrb);
                    }
                }
            }
        }
    }

    public sealed class QualityInfusionOrb : Orb
    {
        public const float speed = 30f;

        public uint maxHpValue;

        private CharacterMasterExtraStatsTracker _targetMasterStats;

        public override void Begin()
        {
            duration = distanceToTarget / 30f;

            EffectData effectData = new EffectData
            {
                origin = origin,
                genericFloat = duration
            };

            effectData.SetHurtBoxReference(target);

            EffectManager.SpawnEffect(OrbStorageUtility.Get("Prefabs/Effects/OrbEffects/InfusionOrbEffect"), effectData, transmit: true);

            HealthComponent targetHealthComponent = target ? target.healthComponent : null;
            CharacterBody targetBody = targetHealthComponent ? targetHealthComponent.body : null;
            CharacterMaster targetMaster = targetBody ? targetBody.master : null;

            _targetMasterStats = targetMaster ? targetMaster.GetComponentCached<CharacterMasterExtraStatsTracker>() : null;
        }

        public override void OnArrival()
        {
            if (_targetMasterStats)
            {
                _targetMasterStats.QualityInfusionBonus += maxHpValue;
            }
        }
    }
}
