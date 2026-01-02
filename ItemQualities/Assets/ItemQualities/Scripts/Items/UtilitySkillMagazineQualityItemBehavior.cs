using RoR2;
using UnityEngine;

namespace ItemQualities.Items
{
    public sealed class UtilitySkillMagazineQualityItemBehavior : QualityItemBodyBehavior
    {
        static EffectIndex _restockEffectIndex = EffectIndex.Invalid;

        [SystemInitializer(typeof(EffectCatalogUtils))]
        static void Init()
        {
            _restockEffectIndex = EffectCatalogUtils.FindEffectIndex("AmmoPackPickupEffect");
            if (_restockEffectIndex == EffectIndex.Invalid)
            {
                Log.Error("Failed to find restock effect index");
            }
        }

        [ItemGroupAssociation(QualityItemBehaviorUsageFlags.Authority)]
        static ItemQualityGroup GetItemGroup()
        {
            return ItemQualitiesContent.ItemQualityGroups.UtilitySkillMagazine;
        }

        Run.FixedTimeStamp _lastUtilitySkillRechargeTime;

        void OnEnable()
        {
            GenericSkillHooks.OnSkillRechargeAuthority += onSkillRechargeAuthority;
            Body.onSkillActivatedAuthority += onSkillActivatedAuthority;

            _lastUtilitySkillRechargeTime = Run.FixedTimeStamp.negativeInfinity;
        }

        void OnDisable()
        {
            GenericSkillHooks.OnSkillRechargeAuthority -= onSkillRechargeAuthority;
            Body.onSkillActivatedAuthority -= onSkillActivatedAuthority;
        }

        void onSkillRechargeAuthority(GenericSkill skill)
        {
            if (Body.skillLocator && skill && skill == Body.skillLocator.utility)
            {
                _lastUtilitySkillRechargeTime = Run.FixedTimeStamp.now;
            }
        }

        void onSkillActivatedAuthority(GenericSkill skill)
        {
            if (Body.inputBank.skill3.justPressed && Body.skillLocator && skill && skill == Body.skillLocator.utility)
            {
                ItemQualityCounts utilitySkillMagazine = Stacks;

                float cooldownRefundWindow = 0.1f;
                float cooldownReductionWindow = cooldownRefundWindow + 0.2f;

                if (_lastUtilitySkillRechargeTime.timeSince <= cooldownRefundWindow)
                {
                    skill.AddOneStock();

                    if (_restockEffectIndex != EffectIndex.Invalid)
                    {
                        EffectManager.SpawnEffect(_restockEffectIndex, new EffectData
                        {
                            origin = Body.corePosition
                        }, true);
                    }
                }
                else if (_lastUtilitySkillRechargeTime.timeSince <= cooldownReductionWindow)
                {
                    float remainingCooldownMultiplier = Mathf.Pow(1f - 0.1f, utilitySkillMagazine.UncommonCount) *
                                                        Mathf.Pow(1f - 0.2f, utilitySkillMagazine.RareCount) *
                                                        Mathf.Pow(1f - 0.3f, utilitySkillMagazine.EpicCount) *
                                                        Mathf.Pow(1f - 0.5f, utilitySkillMagazine.LegendaryCount);

                    skill.rechargeStopwatch += skill.cooldownRemaining * (1f - remainingCooldownMultiplier);
                }
            }
        }
    }
}
