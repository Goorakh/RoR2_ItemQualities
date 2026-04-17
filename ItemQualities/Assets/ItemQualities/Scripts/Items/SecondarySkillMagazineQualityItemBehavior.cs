using ItemQualities.Utilities;
using RoR2;
using RoR2.Skills;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ItemQualities.Items
{
    public sealed class SecondarySkillMagazineQualityItemBehavior : QualityItemBodyBehavior
    {
        static int[] _onActivateBlacklistSkillIndices = Array.Empty<int>();

        static int[] _otherSkillIndicesToRestock = Array.Empty<int>();

        static EffectIndex _restockEffectIndex = EffectIndex.Invalid;

        [SystemInitializer(typeof(EffectCatalogUtils), typeof(SkillCatalog))]
        static void Init()
        {
            _restockEffectIndex = EffectCatalogUtils.FindEffectIndex("AmmoPackPickupEffect");
            if (_restockEffectIndex == EffectIndex.Invalid)
            {
                Log.Error("Failed to find restock effect index");
            }

            List<int> onActivateBlacklistSkillIndices = new List<int>();
            foreach (SkillDef skillDef in SkillCatalog.allSkillDefs)
            {
                if (skillDef.stockToConsume == 0)
                {
                    onActivateBlacklistSkillIndices.Add(skillDef.skillIndex);
                    Log.Debug($"Blacklist {SkillCatalog.GetSkillName(skillDef.skillIndex)}: Doesn't consume stock");
                }
            }

            HashSet<int> otherSkillIndicesToRestock = new HashSet<int>();
            tryAddSkillByName("SnipeHeavy", otherSkillIndicesToRestock);

            static void tryAddSkillByName(string name, ICollection<int> skillIndices)
            {
                int skillIndex = SkillCatalog.FindSkillIndexByName(name);
                if (skillIndex != -1)
                {
                    skillIndices.Add(skillIndex);
                }
            }

            if (onActivateBlacklistSkillIndices.Count > 0)
            {
                _onActivateBlacklistSkillIndices = onActivateBlacklistSkillIndices.ToArray();
                Array.Sort(_onActivateBlacklistSkillIndices);
            }

            if (otherSkillIndicesToRestock.Count > 0)
            {
                _otherSkillIndicesToRestock = otherSkillIndicesToRestock.ToArray();
                Array.Sort(_otherSkillIndicesToRestock);
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
            SecondarySkillMagazine.OnSkillUsedIndirectAuthority += onSkillUsedIndirectAuthority;
        }

        void OnDisable()
        {
            Body.onSkillActivatedAuthority -= onSkillActivatedAuthority;
            SecondarySkillMagazine.OnSkillUsedIndirectAuthority -= onSkillUsedIndirectAuthority;
        }

        void onSkillActivatedAuthority(GenericSkill skill)
        {
            if (Array.BinarySearch(_onActivateBlacklistSkillIndices, skill.skillDef.skillIndex) >= 0)
                return;

            if (Body.skillLocator && skill == Body.skillLocator.secondary)
            {
                rollRestockSkill(skill);
            }
        }

        void onSkillUsedIndirectAuthority(GenericSkill skill)
        {
            if (!Body.skillLocator)
                return;

            if (skill == Body.skillLocator.secondary)
            {
                rollRestockSkill(skill);
            }
        }

        void rollRestockSkill(GenericSkill skill)
        {
            ref readonly ItemQualityCounts secondarySkillMagazine = ref Stacks;

            float freeRestockChanceNormalized = 1f - (Mathf.Pow(1f - 0.15f, secondarySkillMagazine.UncommonCount) *
                                                      Mathf.Pow(1f - 0.25f, secondarySkillMagazine.RareCount) *
                                                      Mathf.Pow(1f - 0.40f, secondarySkillMagazine.EpicCount) *
                                                      Mathf.Pow(1f - 0.60f, secondarySkillMagazine.LegendaryCount));

            if (RollUtil.CheckRoll(freeRestockChanceNormalized * 100f, Body.master, false))
            {
                restockSkill(skill);

                if (_restockEffectIndex != EffectIndex.Invalid)
                {
                    EffectManager.SpawnEffect(_restockEffectIndex, new EffectData
                    {
                        origin = Body.corePosition
                    }, true);
                }

                int skillSlotCount = Body.skillLocator.skillSlotCount;
                for (int i = 0; i < skillSlotCount; i++)
                {
                    GenericSkill otherSkill = Body.skillLocator.GetSkillAtIndex(i);
                    if (otherSkill && otherSkill != skill &&
                        Array.BinarySearch(_otherSkillIndicesToRestock, otherSkill.skillDef.skillIndex) >= 0)
                    {
                        restockSkill(otherSkill);
                    }
                }
            }
        }

        void restockSkill(GenericSkill skill)
        {
            skill.AddOneStock();
            Body.OnSkillCooldown(skill, 1);
        }
    }
}
