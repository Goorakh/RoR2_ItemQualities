using ItemQualities.ModCompatibility;
using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using R2API;
using RoR2;
using System;

namespace ItemQualities.Items
{
    internal static class BearVoid
    {
        [SystemInitializer]
        private static void Init()
        {
            IL.RoR2.HealthComponent.TakeDamageProcess += HealthComponent_TakeDamageProcess;
        }

        public static void TakeDamageModifier(ref float damageValue, HealthComponent victim, DamageInfo damageInfo)
        {
            if (!victim.body)
            {
                return;
            }

            CharacterBody attackerBody = damageInfo.attacker ? damageInfo.attacker.GetComponent<CharacterBody>() : null;
            BodyIndex attackerBodyIndex = attackerBody ? attackerBody.bodyIndex : BodyIndex.None;
            SurvivorIndex attackerSurvivorIndex = SurvivorCatalog.GetSurvivorIndexFromBodyIndex(attackerBodyIndex);

            bool isVoidDamageType = damageInfo.damageType.HasModdedDamageType(DamageTypes.Void);
            bool attackerIsVoidBody = attackerBody && (attackerBody.bodyFlags & CharacterBody.BodyFlags.Void) != 0;

            // If DamageSourceForEnemies is not installed, let any damage count for non-survivor attackers since we (probably) have no tracking for skill damage
            bool attackIsSkillDamageEstimate = damageInfo.damageType.IsDamageSourceSkillBased ||
                                               (!DamageSourceForEnemies.Enabled && (attackerSurvivorIndex == SurvivorIndex.None));

            bool isVoidAttack = isVoidDamageType || (attackerIsVoidBody && attackIsSkillDamageEstimate);
            if (isVoidAttack)
            {
                BuffQualityCounts bearVoidFog = victim.body.GetBuffCounts(ItemQualitiesContent.BuffQualityGroups.BearVoidFog);

                float damageMultiplier = 1f + (bearVoidFog.UncommonCount * 0.03f) +
                                              (bearVoidFog.RareCount * 0.05f) +
                                              (bearVoidFog.EpicCount * 0.08f) +
                                              (bearVoidFog.LegendaryCount * 0.10f);

                if (damageMultiplier > 1)
                {
                    damageValue *= damageMultiplier;
                    damageInfo.damageColorIndex = DamageColorIndex.Void;
                }
            }
        }

        private static void HealthComponent_TakeDamageProcess(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (!c.Method.TryFindParameter<DamageInfo>(out ParameterDefinition damageInfoParameter))
            {
                Log.Error("Failed to find DamageInfo parameter");
                return;
            }

            if (!c.TryGotoNext(MoveType.After,
                               x => x.MatchLdarg(0),
                               x => x.MatchLdfld<HealthComponent>(nameof(HealthComponent.body)),
                               x => x.MatchLdsfld(typeof(DLC1Content.Buffs), nameof(DLC1Content.Buffs.BearVoidReady)),
                               x => x.MatchCallOrCallvirt<CharacterBody>(nameof(CharacterBody.RemoveBuff))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Emit(OpCodes.Ldarg_0);
            c.Emit(OpCodes.Ldarg, damageInfoParameter);
            c.EmitDelegate<Action<HealthComponent, DamageInfo>>(onVoidBearBlock);

            static void onVoidBearBlock(HealthComponent victim, DamageInfo damageInfo)
            {
                if (!victim.body || !victim.body.inventory)
                {
                    return;
                }

                CharacterBody attackerBody = damageInfo.attacker ? damageInfo.attacker.GetComponent<CharacterBody>() : null;
                if (!attackerBody)
                {
                    return;
                }

                ItemQualityCounts bearVoid = victim.body.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.BearVoid);
                if (bearVoid.TotalQualityCount == 0)
                {
                    return;
                }

                QualityTier qualityTier = bearVoid.HighestQuality;

                float fogChance = (bearVoid.UncommonCount * 25f) +
                                  (bearVoid.RareCount * 50f) +
                                  (bearVoid.EpicCount * 75f) +
                                  (bearVoid.LegendaryCount * 100f);

                int fogCount = RollUtil.GetOverflowRoll(fogChance, victim.body.master, false);
                for (int i = 0; i < fogCount; i++)
                {
                    attackerBody.AddBuff(ItemQualitiesContent.BuffQualityGroups.BearVoidFog.GetBuffIndex(qualityTier));
                }
            }
        }
    }
}
