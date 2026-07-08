using ItemQualities.Buffs;
using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using System;
using UnityEngine;
using UnityEngine.Networking;

namespace ItemQualities.Equipments
{
    internal static class CritOnUse
    {
        public const float WeakPointRadius = 1.2f;
        public const float WeakPointRadiusSqr = WeakPointRadius * WeakPointRadius;

        public static float GetCritMultiplierBonus(QualityTier qualityTier)
        {
            switch (qualityTier)
            {
                case QualityTier.None:
                    return 0f;
                case QualityTier.Uncommon:
                    return 0.5f;
                case QualityTier.Rare:
                    return 1.0f;
                case QualityTier.Epic:
                    return 1.5f;
                case QualityTier.Legendary:
                    return 2.5f;
                default:
                    Log.Warning($"Quality tier {qualityTier} is not implemented");
                    return 0f;
            }
        }

        private static bool isWeakPointHit(DamageInfo damageInfo)
        {
            return damageInfo.inflictedHurtbox &&
                   damageInfo.inflictedHurtbox.enabled &&
                   damageInfo.inflictedHurtbox.indexInGroup != -1 &&
                   damageInfo.inflictedHurtbox.healthComponent &&
                   damageInfo.inflictedHurtbox.healthComponent.TryGetComponentCached(out CharacterBodyExtraStatsTracker victimBodyExtraStats) &&
                   damageInfo.inflictedHurtbox.indexInGroup == victimBodyExtraStats.WeakPointHurtBoxIndex;
        }

        [SystemInitializer]
        private static void Init()
        {
            IL.RoR2.EquipmentSlot.FireCritOnUse += EquipmentSlot_FireCritOnUse;

            IL.RoR2.HealthComponent.TakeDamageProcess += IL_HealthComponent_TakeDamageProcess;

            BuffHooks.OnBuffFirstStackGainedGlobal += onBuffFirstStackGainedGlobal;
            BuffHooks.OnBuffFinalStackLostGlobal += onBuffFinalStackLostGlobal;
        }

        private static void onBuffFirstStackGainedGlobal(CharacterBody body, BuffDef buffDef)
        {
            if (!NetworkServer.active)
                return;

            BuffIndex buffIndex = buffDef ? buffDef.buffIndex : BuffIndex.None;
            QualityTier qualityTier = QualityCatalog.GetQualityTier(buffIndex);
            BuffQualityGroupIndex buffGroupIndex = QualityCatalog.FindBuffQualityGroupIndex(buffIndex);

            if (buffGroupIndex != ItemQualitiesContent.BuffQualityGroups.FullCrit.GroupIndex)
                return;

            for (QualityTier buffQualityTier = QualityTier.None; buffQualityTier < QualityTier.Count; buffQualityTier++)
            {
                BuffIndex qualityBuffIndex = QualityCatalog.GetBuffIndexOfQuality(buffIndex, buffQualityTier);
                if (qualityBuffIndex != buffIndex && body.HasBuffRaw(qualityBuffIndex))
                {
                    if (buffQualityTier > qualityTier)
                    {
                        body.ClearTimedBuffsRaw(buffIndex);
                        return;
                    }

                    body.ClearTimedBuffsRaw(qualityBuffIndex);
                }
            }

            if (qualityTier > QualityTier.None)
            {
                QualityCritOnUseAttachment.EnsureAttachment(body);
            }
        }

        private static void onBuffFinalStackLostGlobal(CharacterBody body, BuffDef buffDef)
        {
            if (!NetworkServer.active)
                return;

            BuffIndex buffIndex = buffDef ? buffDef.buffIndex : BuffIndex.None;
            QualityTier qualityTier = QualityCatalog.GetQualityTier(buffIndex);
            BuffQualityGroupIndex buffGroupIndex = QualityCatalog.FindBuffQualityGroupIndex(buffIndex);

            if (buffGroupIndex != ItemQualitiesContent.BuffQualityGroups.FullCrit.GroupIndex || qualityTier == QualityTier.None)
                return;

            for (QualityTier buffQualityTier = 0; buffQualityTier < QualityTier.Count; buffQualityTier++)
            {
                BuffIndex qualityBuffIndex = QualityCatalog.GetBuffIndexOfQuality(buffIndex, buffQualityTier);
                if (qualityBuffIndex != buffIndex && body.HasBuffRaw(qualityBuffIndex))
                {
                    return;
                }
            }

            QualityCritOnUseAttachment qualityCritOnUseAttachment = QualityCritOnUseAttachment.FindAttachment(body);
            if (qualityCritOnUseAttachment)
            {
                GameObject.Destroy(qualityCritOnUseAttachment.gameObject);
            }
        }

        private static void EquipmentSlot_FireCritOnUse(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (c.TryGotoNext(MoveType.After,
                              x => x.MatchLdsfld(typeof(RoR2Content.Buffs), nameof(RoR2Content.Buffs.FullCrit))))
            {
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate<Func<BuffDef, EquipmentSlot, BuffDef>>(getBuff);

                static BuffDef getBuff(BuffDef buffDef, EquipmentSlot equipmentSlot)
                {
                    BuffIndex buffIndex = buffDef ? buffDef.buffIndex : BuffIndex.None;

                    QualityTier qualityTier = equipmentSlot.GetCurrentEquipmentActionQualityTier();
                    if (qualityTier > QualityTier.None)
                    {
                        BuffIndex qualityBuffIndex = QualityCatalog.GetBuffIndexOfQuality(buffIndex, qualityTier);
                        if (qualityBuffIndex != BuffIndex.None && qualityBuffIndex != buffIndex)
                        {
                            buffDef = BuffCatalog.GetBuffDef(qualityBuffIndex);
                            buffIndex = qualityBuffIndex;
                        }
                    }

                    return buffDef;
                }
            }
            else
            {
                Log.Error("Failed to find buff patch location");
            }

            if (c.TryGotoNext(MoveType.Before,
                              x => x.MatchCallOrCallvirt(typeof(CharacterBody), nameof(CharacterBody.AddTimedBuff))))
            {
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate<Func<float, EquipmentSlot, float>>(getDuration);

                static float getDuration(float duration, EquipmentSlot equipmentSlot)
                {
                    QualityTier qualityTier = equipmentSlot.GetCurrentEquipmentActionQualityTier();
                    switch (qualityTier)
                    {
                        case QualityTier.Uncommon:
                            duration += 2f;
                            break;
                        case QualityTier.Rare:
                            duration += 4f;
                            break;
                        case QualityTier.Epic:
                            duration += 6f;
                            break;
                        case QualityTier.Legendary:
                            duration += 8f;
                            break;
                    }

                    return duration;
                }
            }
            else
            {
                Log.Error("Failed to find duration patch location");
            }
        }

        private static void IL_HealthComponent_TakeDamageProcess(ILContext il)
        {
            if (!il.Method.TryFindParameter<DamageInfo>(out ParameterDefinition damageInfoParameter))
            {
                Log.Error("Failed to find DamageInfo parameter");
                return;
            }

            ILCursor c = new ILCursor(il);

            c.Emit(OpCodes.Ldarg, damageInfoParameter);
            c.EmitDelegate<Action<DamageInfo>>(handleWeakPointHit);

            static void handleWeakPointHit(DamageInfo damageInfo)
            {
                if (isWeakPointHit(damageInfo))
                {
                    damageInfo.crit = true;
                    damageInfo.damageColorIndex = DamageColorIndex.WeakPoint;
                }
            }

            int patchCount = 0;

            while (c.TryGotoNext(MoveType.After,
                                 x => x.MatchCallOrCallvirt<CharacterBody>("get_" + nameof(CharacterBody.critMultiplier))))
            {
                c.Emit(OpCodes.Ldarg, damageInfoParameter);
                c.EmitDelegate<Func<float, DamageInfo, float>>(getCritMultiplier);

                static float getCritMultiplier(float critMultiplier, DamageInfo damageInfo)
                {
                    if (isWeakPointHit(damageInfo))
                    {
                        if (damageInfo.inflictedHurtbox &&
                            damageInfo.inflictedHurtbox.healthComponent &&
                            damageInfo.inflictedHurtbox.healthComponent.TryGetComponentCached(out CharacterBodyExtraStatsTracker victimBodyExtraStats) &&
                            victimBodyExtraStats.WeakPointCritMultiplierBonusServer > 0f)
                        {
                            critMultiplier += victimBodyExtraStats.WeakPointCritMultiplierBonusServer;
                        }
                    }

                    return critMultiplier;
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
