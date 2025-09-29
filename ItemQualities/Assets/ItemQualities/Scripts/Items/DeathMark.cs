using HG;
using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using System;

namespace ItemQualities.Items
{
    static class DeathMark
    {
        static readonly BuffIndex[] _qualityDeathMarkDebuffIndices = new BuffIndex[(int)QualityTier.Count];

        [SystemInitializer]
        static void Init()
        {
            _qualityDeathMarkDebuffIndices[(int)QualityTier.Uncommon] = ItemQualitiesContent.Buffs.DeathMarkUncommon.buffIndex;
            _qualityDeathMarkDebuffIndices[(int)QualityTier.Rare] = ItemQualitiesContent.Buffs.DeathMarkRare.buffIndex;
            _qualityDeathMarkDebuffIndices[(int)QualityTier.Epic] = ItemQualitiesContent.Buffs.DeathMarkEpic.buffIndex;
            _qualityDeathMarkDebuffIndices[(int)QualityTier.Legendary] = ItemQualitiesContent.Buffs.DeathMarkLegendary.buffIndex;

            IL.RoR2.HealthComponent.TakeDamageProcess += HealthComponent_TakeDamageProcess;

            IL.RoR2.GlobalEventManager.ProcDeathMark += GlobalEventManager_ProcDeathMark;

            IL.RoR2.CharacterBody.UpdateAllTemporaryVisualEffects += CharacterBody_UpdateAllTemporaryVisualEffects;
        }

        public static bool HasAnyQualityDeathMarkDebuff(CharacterBody body)
        {
            if (body)
            {
                for (QualityTier qualityTier = 0; qualityTier < QualityTier.Count; qualityTier++)
                {
                    BuffIndex qualityDeathMarkDebuffIndex = ArrayUtils.GetSafe(_qualityDeathMarkDebuffIndices, (int)qualityTier, BuffIndex.None);
                    if (qualityDeathMarkDebuffIndex > BuffIndex.None && body.HasBuff(qualityDeathMarkDebuffIndex))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        static void HealthComponent_TakeDamageProcess(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (!il.Method.TryFindParameter<DamageInfo>(out ParameterDefinition damageInfoParameter))
            {
                Log.Error("Failed to find DamageInfo parameter");
                return;
            }

            if (!c.TryFindNext(out ILCursor[] foundCursors,
                               x => x.MatchLdsfld(typeof(RoR2Content.Buffs), nameof(RoR2Content.Buffs.DeathMark)),
                               x => x.MatchCallOrCallvirt<CharacterBody>(nameof(CharacterBody.HasBuff)),
                               x => x.MatchLdcR4(1.5f)))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Goto(foundCursors[1].Next, MoveType.After);

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<bool, HealthComponent, bool>>(hasDeathMarkDebuffHealthComponent);

            static bool hasDeathMarkDebuffHealthComponent(bool hasDebuff, HealthComponent healthComponent)
            {
                return hasDebuff || HasAnyQualityDeathMarkDebuff(healthComponent ? healthComponent.body : null);
            }

            c.Goto(foundCursors[2].Next, MoveType.After);

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<float, HealthComponent, float>>(getDeathMarkDamageMultiplier);

            static float getDeathMarkDamageMultiplier(float baseDeathMarkDamageMultiplier, HealthComponent healthComponent)
            {
                CharacterBody body = healthComponent ? healthComponent.body : null;
                if (!body)
                    return baseDeathMarkDamageMultiplier;

                float deathMarkDamageMultiplier = 1f;

                if (body.HasBuff(RoR2Content.Buffs.DeathMark))
                {
                    deathMarkDamageMultiplier += baseDeathMarkDamageMultiplier - 1f;
                }

                if (body.HasBuff(ItemQualitiesContent.Buffs.DeathMarkUncommon))
                {
                    deathMarkDamageMultiplier += 0.2f;
                }

                if (body.HasBuff(ItemQualitiesContent.Buffs.DeathMarkRare))
                {
                    deathMarkDamageMultiplier += 0.5f;
                }

                if (body.HasBuff(ItemQualitiesContent.Buffs.DeathMarkEpic))
                {
                    deathMarkDamageMultiplier += 1.0f;
                }

                if (body.HasBuff(ItemQualitiesContent.Buffs.DeathMarkLegendary))
                {
                    deathMarkDamageMultiplier += 1.5f;
                }

                return deathMarkDamageMultiplier;
            }
        }

        static void GlobalEventManager_ProcDeathMark(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (!il.Method.TryFindParameter<CharacterMaster>("attackerMaster", out ParameterDefinition attackerMasterParameter))
            {
                Log.Error("Failed to find attackerMaster parameter");
                return;
            }

            if (!il.Method.TryFindParameter<CharacterBody>("victimBody", out ParameterDefinition victimBodyParameter))
            {
                Log.Error("Failed to find attackerMaster parameter");
                return;
            }

            if (!c.TryFindNext(out ILCursor[] foundCursors,
                               x => x.MatchLdsfld(typeof(RoR2Content.Buffs), nameof(RoR2Content.Buffs.DeathMark)),
                               x => x.MatchCallOrCallvirt<CharacterBody>(nameof(CharacterBody.HasBuff)),
                               x => x.MatchBrtrue(out _)))
            {
                Log.Error("Failed to find DeathMark buff check location");
                return;
            }

            c.Goto(foundCursors[2].Next, MoveType.After);
            ILLabel deathMarkBlockStartLabel = c.MarkLabel();

            c.Goto(foundCursors[2].Next, MoveType.Before);
            c.Emit(OpCodes.Brfalse, deathMarkBlockStartLabel);

            c.Emit(OpCodes.Ldarg, attackerMasterParameter);
            c.Emit(OpCodes.Ldarg, victimBodyParameter);
            c.EmitDelegate<Func<CharacterMaster, CharacterBody, bool>>(disallowQualityDeathMarkProc);
            // delegate return goes into original brtrue (foundCursors[2])

            static bool disallowQualityDeathMarkProc(CharacterMaster attackerMaster, CharacterBody victimBody)
            {
                return !allowQualityDeathMarkProc(attackerMaster, victimBody);
            }

            static bool allowQualityDeathMarkProc(CharacterMaster attackerMaster, CharacterBody victimBody)
            {
                if (!attackerMaster || !victimBody)
                    return false;

                Inventory attackerInventory = attackerMaster.inventory;
                ItemQualityCounts deathMark = ItemQualitiesContent.ItemQualityGroups.DeathMark.GetItemCounts(attackerInventory);
                if (deathMark.TotalQualityCount > 0)
                {
                    if (victimBody.TryGetComponent(out CharacterBodyExtraStatsTracker victimBodyExtraStats) &&
                        !victimBodyExtraStats.HasHadAnyQualityDeathMarkDebuffServer)
                    {
                        return true;
                    }
                }

                return false;
            }

            if (!c.TryGotoNext(MoveType.After,
                               x => x.MatchCallOrCallvirt<CharacterBody>(nameof(CharacterBody.AddTimedBuff))))
            {
                Log.Error("Failed to find DeathMark buff apply location");
                return;
            }

            Instruction addTimedBuffCallInstruction = c.Prev;

            int debuffCountLocalIndex = -1;
            if (!c.TryFindPrev(out foundCursors,
                               x => x.MatchLdloc(typeof(int), il, out debuffCountLocalIndex),
                               x => x.MatchLdcI4(4),
                               x => x.MatchBlt(out _)))
            {
                Log.Error("Failed to find debuffCount variable");
                return;
            }

            c.Goto(addTimedBuffCallInstruction, MoveType.After);

            c.Emit(OpCodes.Ldloc, debuffCountLocalIndex);
            c.Emit(OpCodes.Ldarg, attackerMasterParameter);
            c.Emit(OpCodes.Ldarg, victimBodyParameter);
            c.EmitDelegate<Action<int, CharacterMaster, CharacterBody>>(tryQualityDeathMarkProc);

            static void tryQualityDeathMarkProc(int debuffCount, CharacterMaster attackerMaster, CharacterBody victimBody)
            {
                if (debuffCount >= 7 && allowQualityDeathMarkProc(attackerMaster, victimBody))
                {
                    ItemQualityCounts deathMark = ItemQualitiesContent.ItemQualityGroups.DeathMark.GetItemCounts(attackerMaster.inventory);

                    QualityTier highestDeathMarkQuality = QualityTier.None;
                    for (QualityTier qualityTier = QualityTier.Count - 1; qualityTier >= 0; qualityTier--)
                    {
                        ItemIndex qualityDeathMarkItemIndex = ItemQualitiesContent.ItemQualityGroups.DeathMark.GetItemIndex(qualityTier);
                        if (attackerMaster.inventory.GetItemCount(qualityDeathMarkItemIndex) > 0)
                        {
                            highestDeathMarkQuality = qualityTier;
                            break;
                        }
                    }

                    if (highestDeathMarkQuality > QualityTier.None)
                    {
                        BuffIndex qualityDeathMarkDebuffIndex = ArrayUtils.GetSafe(_qualityDeathMarkDebuffIndices, (int)highestDeathMarkQuality, BuffIndex.None);

                        if (qualityDeathMarkDebuffIndex > BuffIndex.None)
                        {
                            float qualityDeathMarkDuration = (2f * deathMark.UncommonCount) +
                                                             (4f * deathMark.RareCount) +
                                                             (5f * deathMark.EpicCount) +
                                                             (7f * deathMark.LegendaryCount);

                            victimBody.AddTimedBuff(qualityDeathMarkDebuffIndex, qualityDeathMarkDuration);
                        }
                    }
                }
            }
        }

        static void CharacterBody_UpdateAllTemporaryVisualEffects(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (!c.TryFindNext(out ILCursor[] foundCursors,
                               x => x.MatchLdsfld(typeof(RoR2Content.Buffs), nameof(RoR2Content.Buffs.DeathMark)),
                               x => x.MatchCallOrCallvirt<CharacterBody>(nameof(CharacterBody.HasBuff))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Goto(foundCursors[1].Next, MoveType.After);

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<bool, CharacterBody, bool>>(hasDeathMarkDebuff);
            static bool hasDeathMarkDebuff(bool hasDebuff, CharacterBody body)
            {
                return hasDebuff || HasAnyQualityDeathMarkDebuff(body);
            }
        }
    }
}
