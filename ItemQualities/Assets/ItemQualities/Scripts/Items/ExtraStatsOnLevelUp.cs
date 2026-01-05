using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using System;
using UnityEngine.Networking;

namespace ItemQualities.Items
{
    // Because regular Prayer Beads are horribly made, this code has to inherit that
    static class ExtraStatsOnLevelUp
    {
        [SystemInitializer]
        static void Init()
        {
            IL.RoR2.CharacterBody.RecalculateStats += CharacterBody_RecalculateStats;
        }

        static void CharacterBody_RecalculateStats(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            int extraStatsOnLevelUpItemCountVar = -1;
            if (!c.TryGotoNext(MoveType.After,
                               x => x.MatchLdsfld(typeof(DLC2Content.Items), nameof(DLC2Content.Items.ExtraStatsOnLevelUp)),
                               x => x.MatchCallOrCallvirt<Inventory>(nameof(Inventory.GetItemCountPermanent)),
                               x => x.MatchStloc(typeof(int), il, out extraStatsOnLevelUpItemCountVar)))
            {
                Log.Error("Failed to find Prayer Beads item count variable");
                return;
            }

            c.Emit(OpCodes.Ldloc, extraStatsOnLevelUpItemCountVar);
            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<CharacterBody, int>>(getQualityItemCount);
            c.Emit(OpCodes.Add);
            c.Emit(OpCodes.Stloc, extraStatsOnLevelUpItemCountVar);

            static int getQualityItemCount(CharacterBody body)
            {
                if (!body || !body.inventory)
                    return 0;

                ItemQualityCounts extraStatsOnLevelUp = body.inventory.GetItemCountsPermanent(ItemQualitiesContent.ItemQualityGroups.ExtraStatsOnLevelUp);
                return extraStatsOnLevelUp.TotalQualityCount;
            }

            int beadResetPatchCount = 0;

            c.Index = 0;
            while (c.TryGotoNext(MoveType.After,
                                 x => x.MatchStfld<CharacterBody>(nameof(CharacterBody.extraStatsOnLevelUpCount_CachedLastApplied))))
            {
                c.MoveBeforeLabels();

                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate<Action<CharacterBody>>(recordBeadCount);

                static void recordBeadCount(CharacterBody body)
                {
                    if (body && body.inventory && body.TryGetComponentCached(out CharacterBodyExtraStatsTracker bodyExtraStats))
                    {
                        bodyExtraStats.LastExtraStatsOnLevelUpCounts = body.inventory.GetItemCountsPermanent(ItemQualitiesContent.ItemQualityGroups.ExtraStatsOnLevelUp);
                    }
                }

                beadResetPatchCount++;
            }

            if (beadResetPatchCount == 0)
            {
                Log.Error("Failed to find bead reset patch location");
            }
            else
            {
                Log.Debug($"Found {beadResetPatchCount} bead reset patch location(s)");
            }

            c.Index = 0;

            if (!c.TryGotoNext(MoveType.After,
                               x => x.MatchLdfld(out FieldReference field) && field?.Name == "levelUpBuffCount",
                               x => x.MatchLdcI4(0),
                               x => x.MatchBle(out _)))
            {
                Log.Error("Failed to find Prayer Bead proc patch location");
                return;
            }

            VariableDefinition packedLevelBonusesVar = il.AddVariable<uint>();

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<CharacterBody, uint>>(tryProcQualityBeads);
            c.Emit(OpCodes.Stloc, packedLevelBonusesVar);

            static uint tryProcQualityBeads(CharacterBody body)
            {
                if (!NetworkServer.active)
                    return 0;

                if (!body || !body.isPlayerControlled || !body.inventory || !body.TryGetComponentCached(out CharacterBodyExtraStatsTracker bodyExtraStats))
                    return 0;

                ItemQualityCounts extraStatsOnLevelUp = body.inventory.GetItemCountsPermanent(ItemQualitiesContent.ItemQualityGroups.ExtraStatsOnLevelUp);

                ItemQualityCounts beadsSpent = bodyExtraStats.LastExtraStatsOnLevelUpCounts - extraStatsOnLevelUp;
                if (beadsSpent.TotalQualityCount <= 0)
                    return 0;

                int playerLevelBonus = (1 * beadsSpent.UncommonCount) +
                                       (2 * beadsSpent.RareCount) +
                                       (3 * beadsSpent.EpicCount) +
                                       (5 * beadsSpent.LegendaryCount);

                int ambientLevelPenalty = (5 * beadsSpent.UncommonCount) +
                                          (7 * beadsSpent.RareCount) +
                                          (10 * beadsSpent.EpicCount) +
                                          (15 * beadsSpent.LegendaryCount);

                if (playerLevelBonus > 0)
                {
                    uint currentLevel = TeamManager.instance.GetTeamLevel(body.teamComponent.teamIndex);

                    ulong currentLevelExperience = TeamManager.GetExperienceForLevel(currentLevel);
                    ulong targetLevelExperience = TeamManager.GetExperienceForLevel(currentLevel + (uint)playerLevelBonus);

                    if (targetLevelExperience > currentLevelExperience)
                    {
                        ulong experienceToGive = targetLevelExperience - currentLevelExperience;

                        if (body.teamComponent.teamIndex == TeamIndex.Player)
                        {
                            PrayerBeadsIgnoreXp.IgnoreXpGain(experienceToGive, ExperienceManager.maxOrbTravelTime + 0.2f);
                        }

                        ExperienceManager.instance.AwardExperience(body.footPosition, body, experienceToGive);
                    }
                }

                if (ambientLevelPenalty > 0 && RunExtraStatsTracker.Instance)
                {
                    RunExtraStatsTracker.Instance.AmbientLevelPenalty += ambientLevelPenalty;
                }

                return PackLevelBonuses(playerLevelBonus, ambientLevelPenalty);
            }

            if (c.TryGotoNext(MoveType.After,
                              x => x.MatchLdsfld(typeof(CharacterBody.CommonAssets), nameof(CharacterBody.CommonAssets.prayerBeadEffect)),
                              x => x.MatchNewobj<EffectData>()))
            {
                c.Emit(OpCodes.Dup);
                c.Emit(OpCodes.Ldloc, packedLevelBonusesVar);
                c.EmitDelegate<Action<EffectData, uint>>(addQualityInfoToEffect);

                static void addQualityInfoToEffect(EffectData effectData, uint packedLevelBonuses)
                {
                    effectData.genericUInt = packedLevelBonuses;
                }
            }
            else
            {
                Log.Warning("Failed to find prayer bead effect patch location");
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
