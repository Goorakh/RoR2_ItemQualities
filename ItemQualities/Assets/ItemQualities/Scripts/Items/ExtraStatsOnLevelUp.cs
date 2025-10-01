using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using System;
using UnityEngine;
using UnityEngine.Networking;

namespace ItemQualities.Items
{
    // Because regular Prayer Beads are horribly made, this code has to inherit that
    static class ExtraStatsOnLevelUp
    {
        [SystemInitializer]
        static void Init()
        {
            IL.RoR2.CharacterMaster.TrackBeadExperience += ItemHooks.CombineGroupedItemCountsPatch;

            IL.RoR2.CharacterBody.RecalculateStats += CharacterBody_RecalculateStats;
        }

        static void CharacterBody_RecalculateStats(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (!c.TryFindNext(out ILCursor[] foundCursors,
                               x => x.MatchLdfld<CharacterBody>(nameof(CharacterBody.extraStatsOnLevelUpCount_CachedLastApplied)),
                               x => x.MatchStfld<CharacterBody>(nameof(CharacterBody.extraStatsOnLevelUpCount_CachedLastApplied)),
                               x => x.MatchStfld(out FieldReference field) && field?.Name == "levelUpBuffCount",
                               x => x.MatchNewobj<EffectData>()))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Goto(foundCursors[1].Next, MoveType.After);
            c.MoveBeforeLabels();

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Action<CharacterBody>>(recordBeadCount);

            static void recordBeadCount(CharacterBody body)
            {
                if (body && body.TryGetComponent(out CharacterBodyExtraStatsTracker bodyExtraStats))
                {
                    bodyExtraStats.LastExtraStatsOnLevelUpCounts = ItemQualitiesContent.ItemQualityGroups.ExtraStatsOnLevelUp.GetItemCounts(body.inventory);
                }
            }

            c.Goto(foundCursors[2].Next, MoveType.After); // stfld levelUpBuffCount

            VariableDefinition packedLevelBonusesVar = il.AddVariable<uint>();

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<CharacterBody, uint>>(tryProcQualityBeads);
            c.Emit(OpCodes.Stloc, packedLevelBonusesVar);

            static uint tryProcQualityBeads(CharacterBody body)
            {
                if (!body || !body.isPlayerControlled || !body.TryGetComponent(out CharacterBodyExtraStatsTracker bodyExtraStats))
                    return PackLevelBonuses(0, 0);

                ItemQualityCounts extraStatsOnLevelUp = ItemQualitiesContent.ItemQualityGroups.ExtraStatsOnLevelUp.GetItemCounts(body.inventory);

                ItemQualityCounts beadsSpent = bodyExtraStats.LastExtraStatsOnLevelUpCounts - extraStatsOnLevelUp;

                bodyExtraStats.LastExtraStatsOnLevelUpCounts = extraStatsOnLevelUp;

                int playerLevelBonus = 0;
                int ambientLevelPenalty = 0;
                if (body.HasBuff(DLC2Content.Buffs.ExtraStatsOnLevelUpBuff))
                {
                    playerLevelBonus = (1 * beadsSpent.UncommonCount) +
                                       (2 * beadsSpent.RareCount) +
                                       (3 * beadsSpent.EpicCount) +
                                       (5 * beadsSpent.LegendaryCount);

                    ambientLevelPenalty = (1 * beadsSpent.UncommonCount) +
                                          (2 * beadsSpent.RareCount) +
                                          (3 * beadsSpent.EpicCount) +
                                          (5 * beadsSpent.LegendaryCount);
                }

                if (NetworkServer.active)
                {
                    if (playerLevelBonus > 0)
                    {
                        uint currentLevel = TeamManager.instance.GetTeamLevel(TeamIndex.Player);

                        ulong currentLevelExperience = TeamManager.GetExperienceForLevel(currentLevel);
                        ulong targetLevelExperience = TeamManager.GetExperienceForLevel(currentLevel + (uint)playerLevelBonus);

                        if (targetLevelExperience > currentLevelExperience)
                        {
                            ExperienceManager.instance.AwardExperience(body.footPosition, body, targetLevelExperience - currentLevelExperience);
                        }
                    }

                    if (ambientLevelPenalty > 0)
                    {
                        if (Run.instance.TryGetComponent(out RunExtraStatsTracker runExtraStats))
                        {
                            runExtraStats.AmbientLevelPenalty += ambientLevelPenalty;
                        }
                    }
                }

                return PackLevelBonuses(playerLevelBonus, ambientLevelPenalty);
            }

            c.Goto(foundCursors[3].Next, MoveType.After); // newobj EffectData..ctor

            c.Emit(OpCodes.Dup);
            c.Emit(OpCodes.Ldloc, packedLevelBonusesVar);
            c.EmitDelegate<Action<EffectData, uint>>(addQualityInfoToEffect);

            static void addQualityInfoToEffect(EffectData effectData, uint packedLevelBonuses)
            {
                effectData.genericUInt = packedLevelBonuses;
            }
        }

        public static uint PackLevelBonuses(int playerLevelBonus, int ambientLevelPenalty)
        {
            ushort low = (ushort)(playerLevelBonus & 0xFFFF);
            ushort high = (ushort)(ambientLevelPenalty & 0xFFFF);
            return ((uint)high << 16) | low;
        }

        public static (int playerLevelBonus, int ambientLevelPenalty) UnpackLevelBonuses(uint packedLevelBonuses)
        {
            int playerLevelBonus = (short)(packedLevelBonuses & 0xFFFF);
            int ambientLevelPenalty = (short)((packedLevelBonuses >> 16) & 0xFFFF);
            return (playerLevelBonus, ambientLevelPenalty);
        }
    }
}
