using RoR2;
using UnityEngine;

namespace ItemQualities.Items
{
    public sealed class SecondarySkillMagazineQualityItemBehavior : MonoBehaviour
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

        void Awake()
        {
            _body = GetComponent<CharacterBody>();
        }

        void OnEnable()
        {
            _body.onSkillActivatedAuthority += onSkillActivatedAuthority;
        }

        void OnDisable()
        {
            _body.onSkillActivatedAuthority -= onSkillActivatedAuthority;
        }

        void onSkillActivatedAuthority(GenericSkill skill)
        {
            if (_body.skillLocator && skill && skill == _body.skillLocator.secondary)
            {
                ItemQualityCounts secondarySkillMagazine = ItemQualitiesContent.ItemQualityGroups.SecondarySkillMagazine.GetItemCountsEffective(_body.inventory);

                float freeRestockChance = (10f * secondarySkillMagazine.UncommonCount) +
                                          (20f * secondarySkillMagazine.RareCount) +
                                          (35f * secondarySkillMagazine.EpicCount) +
                                          (60f * secondarySkillMagazine.LegendaryCount);

                if (Util.CheckRoll(Util.ConvertAmplificationPercentageIntoReductionPercentage(freeRestockChance), _body.master))
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
            }
        }
    }
}
