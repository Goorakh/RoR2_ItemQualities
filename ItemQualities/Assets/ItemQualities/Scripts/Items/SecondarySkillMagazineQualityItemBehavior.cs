using ItemQualities.Utilities;
using RoR2;
using UnityEngine;

namespace ItemQualities.Items
{
    public sealed class SecondarySkillMagazineQualityItemBehavior : QualityItemBodyBehavior
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
            return ItemQualitiesContent.ItemQualityGroups.SecondarySkillMagazine;
        }

        void OnEnable()
        {
            Body.onSkillActivatedAuthority += onSkillActivatedAuthority;
        }

        void OnDisable()
        {
            Body.onSkillActivatedAuthority -= onSkillActivatedAuthority;
        }

        void onSkillActivatedAuthority(GenericSkill skill)
        {
            if (Body.skillLocator && skill && skill == Body.skillLocator.secondary)
            {
                ItemQualityCounts secondarySkillMagazine = Stacks;

                float freeRestockChanceNormalized = 1f - (Mathf.Pow(1f - 0.15f, secondarySkillMagazine.UncommonCount) *
                                                          Mathf.Pow(1f - 0.25f, secondarySkillMagazine.RareCount) *
                                                          Mathf.Pow(1f - 0.40f, secondarySkillMagazine.EpicCount) *
                                                          Mathf.Pow(1f - 0.60f, secondarySkillMagazine.LegendaryCount));

                if (RollUtil.CheckRoll(freeRestockChanceNormalized * 100f, Body.master, false))
                {
                    skill.AddOneStock();
                    Body.OnSkillCooldown(skill, 1);

                    if (_restockEffectIndex != EffectIndex.Invalid)
                    {
                        EffectManager.SpawnEffect(_restockEffectIndex, new EffectData
                        {
                            origin = Body.corePosition
                        }, true);
                    }
                }
            }
        }
    }
}
