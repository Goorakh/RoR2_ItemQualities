using HG;
using ItemQualities.Orbs;
using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.Utils;
using RoR2;
using RoR2.Items;
using RoR2.Orbs;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace ItemQualities.Items
{
    static class ImmuneToDebuff
    {
        [SystemInitializer]
        static void Init()
        {
            On.RoR2.Items.ImmuneToDebuffBehavior.OverrideDot += ImmuneToDebuffBehavior_OverrideDot;

            IL.RoR2.CharacterBody.AddTimedBuff_BuffDef_float += handleDebuffBuffReflectPatch;
            IL.RoR2.CharacterBody.AddTimedBuff_BuffDef_float_int += handleDebuffBuffReflectPatch;
            IL.RoR2.CharacterBody.AddTimedBuffDontRefreshDuration += handleDebuffBuffReflectPatch;
            IL.RoR2.CharacterBody.ExtendTimedBuffIfPresent_BuffDef_float_float += handleDebuffBuffReflectPatch;
        }

        static void trySpreadBlockedDebuff(CharacterBody victimBody, BuffIndex buffIndex, float duration, InflictDotInfo? inflictDotInfo)
        {
            if (!NetworkServer.active)
            {
                Log.Warning("Called on client");
                return;
            }

            if (!victimBody || !victimBody.inventory)
                return;

            if (duration <= 0f && !inflictDotInfo.HasValue)
                return;

            if (inflictDotInfo.HasValue)
            {
                if (buffIndex == BuffIndex.None)
                {
                    DotController.DotDef dotDef = DotController.GetDotDef(inflictDotInfo.Value.dotIndex);
                    if (dotDef != null && dotDef.associatedBuff)
                    {
                        buffIndex = dotDef.associatedBuff.buffIndex;
                    }
                }

                if (duration <= 0f)
                {
                    duration = inflictDotInfo.Value.duration;
                }
            }

            BuffDef buffDef = BuffCatalog.GetBuffDef(buffIndex);
            if (!buffDef || (buffDef.flags & BuffDef.Flags.ExcludeFromNoxiousThorns) != 0)
                return;

            ItemQualityCounts immuneToDebuff = victimBody.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.ImmuneToDebuff);
            if (immuneToDebuff.TotalQualityCount == 0)
                return;

            float spreadRadius = 25f + (5f * immuneToDebuff.UncommonCount) +
                                       (10f * immuneToDebuff.RareCount) +
                                       (30f * immuneToDebuff.EpicCount) +
                                       (50f * immuneToDebuff.LegendaryCount);

            SphereSearch targetSearch = new SphereSearch
            {
                mask = LayerIndex.entityPrecise.mask,
                origin = victimBody.corePosition,
                radius = spreadRadius,
                queryTriggerInteraction = QueryTriggerInteraction.Ignore
            };

            using var _ = ListPool<HurtBox>.RentCollection(out List<HurtBox> targetHurtBoxes);

            targetSearch.RefreshCandidates()
                        .FilterCandidatesByHurtBoxTeam(TeamMask.GetEnemyTeams(victimBody.teamComponent.teamIndex))
                        .FilterCandidatesByDistinctHurtBoxEntities()
                        .OrderCandidatesByDistance()
                        .GetHurtBoxes(targetHurtBoxes);

            foreach (HurtBox targetHurtBox in targetHurtBoxes)
            {
                HealthComponent targetHealthComponent = targetHurtBox ? targetHurtBox.healthComponent : null;
                CharacterBody targetBody = targetHealthComponent ? targetHealthComponent.body : null;
                if (targetBody && targetBody != victimBody)
                {
                    ImmuneToDebuffOrb orb;
                    if (inflictDotInfo.HasValue)
                    {
                        InflictDotInfo victimDotInfo = inflictDotInfo.Value;
                        victimDotInfo.attackerObject = victimBody.gameObject;
                        victimDotInfo.victimObject = targetBody.gameObject;

                        orb = new ImmuneToDebuffOrb
                        {
                            origin = victimBody.corePosition,
                            target = targetHurtBox,
                            InflictDotInfo = victimDotInfo,
                            Attacker = victimDotInfo.attackerObject,
                            BuffStackCount = 1,
                        };
                    }
                    else
                    {
                        orb = new ImmuneToDebuffOrb
                        {
                            origin = victimBody.corePosition,
                            target = targetHurtBox,
                            BuffDuration = duration,
                            BuffIndex = buffIndex,
                            BuffStackCount = 1,
                            Attacker = victimBody.gameObject,
                        };
                    }

                    OrbManager.instance.AddOrb(orb);
                }
            }
        }

        static bool ImmuneToDebuffBehavior_OverrideDot(On.RoR2.Items.ImmuneToDebuffBehavior.orig_OverrideDot orig, InflictDotInfo inflictDotInfo)
        {
            bool blocked = orig(inflictDotInfo);

            if (blocked)
            {
                CharacterBody victimBody = inflictDotInfo.victimObject ? inflictDotInfo.victimObject.GetComponent<CharacterBody>() : null;

                trySpreadBlockedDebuff(victimBody, BuffIndex.None, 0f, inflictDotInfo);
            }

            return blocked;
        }

        static void handleDebuffBuffReflectPatch(ILContext il)
        {
            if (!il.Method.TryFindParameter<float>("duration", out ParameterDefinition durationParameter))
            {
                durationParameter = null;
            }

            ILCursor c = new ILCursor(il);

            int patchCount = 0;

            VariableDefinition bodyTempVar = il.AddVariable<CharacterBody>();

            VariableDefinition buffDefTempVar = null;
            VariableDefinition buffIndexTempVar = null;

            while (c.TryGotoNext(MoveType.Before,
                                 x => x.MatchCallOrCallvirt<ImmuneToDebuffBehavior>(nameof(ImmuneToDebuffBehavior.OverrideDebuff))))
            {
                MethodReference overrideDebuffMethod = (MethodReference)c.Next.Operand;
                bool isBuffIndexOverload = overrideDebuffMethod.Parameters[0].ParameterType.Is(typeof(BuffIndex));

                if (isBuffIndexOverload)
                {
                    buffIndexTempVar ??= il.AddVariable<BuffIndex>();
                }
                else
                {
                    buffDefTempVar ??= il.AddVariable<BuffDef>();
                }

                c.EmitStoreStack(isBuffIndexOverload ? buffIndexTempVar : buffDefTempVar, bodyTempVar);

                c.Index++;

                c.Emit(OpCodes.Dup);
                c.Emit(OpCodes.Ldloc, bodyTempVar);
                c.Emit(OpCodes.Ldloc, isBuffIndexOverload ? buffIndexTempVar : buffDefTempVar);
                if (isBuffIndexOverload)
                {
                    c.EmitDelegate<Func<BuffIndex, BuffDef>>(BuffCatalog.GetBuffDef);
                }

                if (durationParameter != null)
                {
                    c.Emit(OpCodes.Ldarg, durationParameter);
                }
                else
                {
                    c.Emit(OpCodes.Ldc_R4, 8f);
                }

                c.EmitDelegate<Action<bool, CharacterBody, BuffDef, float>>(onOverrideDebuff);

                static void onOverrideDebuff(bool blocked, CharacterBody body, BuffDef buffDef, float duration)
                {
                    if (blocked && buffDef && buffDef.isDebuff)
                    {
                        trySpreadBlockedDebuff(body, buffDef.buffIndex, duration, null);
                    }
                }

                patchCount++;
            }

            if (patchCount == 0)
            {
                Log.Error("Failed to find patch location");
            }
            else
            {
                Log.Debug($"Found {patchCount} patch location(s)");
            }
        }
    }
}
