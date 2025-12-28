using ItemQualities.Utilities;
using RoR2;

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

                float freeRestockChance = (10f * secondarySkillMagazine.UncommonCount) +
                                          (20f * secondarySkillMagazine.RareCount) +
                                          (35f * secondarySkillMagazine.EpicCount) +
                                          (60f * secondarySkillMagazine.LegendaryCount);

                if (RollUtil.CheckRoll(Util.ConvertAmplificationPercentageIntoReductionPercentage(freeRestockChance), Body.master, false))
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
            }
        }
    }
}
