using ItemQualities.Utilities.Extensions;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using R2API;
using RoR2;
using System;

namespace ItemQualities.Equipments
{
    internal static class TeamWarCry
    {
        [SystemInitializer]
        private static void Init()
        {
            On.RoR2.EquipmentSlot.FireTeamWarCry += On_EquipmentSlot_FireTeamWarCry;
            IL.RoR2.EquipmentSlot.FireTeamWarCry += IL_EquipmentSlot_FireTeamWarCry;

            RecalculateStatsAPI.GetStatCoefficients += getStatCoefficients;
        }

        private static void getStatCoefficients(CharacterBody sender, RecalculateStatsAPI.StatHookEventArgs args)
        {
            BuffQualityCounts teamWarCry = sender.GetBuffCounts(ItemQualitiesContent.BuffQualityGroups.TeamWarCry);
            QualityTier qualityTier = teamWarCry.HighestQuality;
            if (qualityTier == QualityTier.None)
                return;

            float cooldownReduction = qualityTier switch
            {
                QualityTier.Uncommon => 2f,
                QualityTier.Rare => 5f,
                QualityTier.Epic => 8f,
                QualityTier.Legendary => 15f,
                _ => throw new NotImplementedException(),
            };

            args.allSkills.cooldownFlatReduction += cooldownReduction;
        }

        private static bool On_EquipmentSlot_FireTeamWarCry(On.RoR2.EquipmentSlot.orig_FireTeamWarCry orig, EquipmentSlot self)
        {
            bool success = orig(self);

            if (success)
            {
                QualityTier qualityTier = self.GetCurrentEquipmentActionQualityTier();
                if (qualityTier != QualityTier.None)
                {
                    foreach (TeamComponent item in TeamComponent.GetTeamMembers(self.characterBody.teamComponent.teamIndex))
                    {
                        foreach (GenericSkill skill in item.body.skillLocator.AllSkills)
                        {
                            if (skill.stock < skill.maxStock)
                            {
                                skill.AddOneStock();
                                item.body.OnSkillCooldown(skill);
                            }
                        }
                    }
                }
            }

            return success;
        }

        private static void IL_EquipmentSlot_FireTeamWarCry(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            int patchCount = 0;
            while (c.TryGotoNext(MoveType.After,
                                 x => x.MatchLdsfld(typeof(RoR2Content.Buffs), nameof(RoR2Content.Buffs.TeamWarCry))))
            {
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate<Func<BuffDef, EquipmentSlot, BuffDef>>(getBuff);

                static BuffDef getBuff(BuffDef buffDef, EquipmentSlot equipmentSlot)
                {
                    QualityTier qualityTier = equipmentSlot.GetCurrentEquipmentActionQualityTier();
                    if (qualityTier > QualityTier.None)
                    {
                        BuffIndex buffIndex = buffDef ? buffDef.buffIndex : BuffIndex.None;

                        BuffIndex qualityBuffIndex = QualityCatalog.GetBuffIndexOfQuality(buffIndex, qualityTier);
                        if (qualityBuffIndex != BuffIndex.None && qualityBuffIndex != buffIndex)
                        {
                            buffDef = BuffCatalog.GetBuffDef(qualityBuffIndex);
                            buffIndex = qualityBuffIndex;
                        }
                    }

                    return buffDef;
                }

                patchCount++;
            }

            if (patchCount == 0)
            {
                Log.Warning("Failed to find patch location");
            }
            else
            {
                Log.Debug($"Found {patchCount} patch location(s)");
            }
        }
    }
}
