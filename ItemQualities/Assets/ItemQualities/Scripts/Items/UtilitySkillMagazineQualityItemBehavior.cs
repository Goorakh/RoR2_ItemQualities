using RoR2;
using UnityEngine;

namespace ItemQualities.Items
{
    public sealed class UtilitySkillMagazineQualityItemBehavior : MonoBehaviour
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

        CharacterBody _body;

        Run.FixedTimeStamp _lastUtilitySkillRechargeTime;

        void Awake()
        {
            _body = GetComponent<CharacterBody>();
        }

        void OnEnable()
        {
            GenericSkillHooks.OnSkillRechargeAuthority += onSkillRechargeAuthority;
            _body.onSkillActivatedAuthority += onSkillActivatedAuthority;

            _lastUtilitySkillRechargeTime = Run.FixedTimeStamp.negativeInfinity;
        }

        void OnDisable()
        {
            GenericSkillHooks.OnSkillRechargeAuthority -= onSkillRechargeAuthority;
            _body.onSkillActivatedAuthority -= onSkillActivatedAuthority;
        }

        void onSkillRechargeAuthority(GenericSkill skill)
        {
            if (_body.skillLocator && skill && skill == _body.skillLocator.utility)
            {
                _lastUtilitySkillRechargeTime = Run.FixedTimeStamp.now;
            }
        }

        void onSkillActivatedAuthority(GenericSkill skill)
        {
            if (_body.inputBank.skill3.justPressed && _body.skillLocator && skill && skill == _body.skillLocator.utility)
            {
                ItemQualityCounts utilitySkillMagazine = ItemQualitiesContent.ItemQualityGroups.UtilitySkillMagazine.GetItemCountsEffective(_body.inventory);

                float cooldownRefundWindow = 0.1f;
                float cooldownReductionWindow = cooldownRefundWindow + 0.2f;

                if (_lastUtilitySkillRechargeTime.timeSince <= cooldownRefundWindow)
                {
                    skill.AddOneStock();

                    if (_restockEffectIndex != EffectIndex.Invalid)
                    {
                        EffectManager.SpawnEffect(_restockEffectIndex, new EffectData
                        {
                            origin = _body.corePosition
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
