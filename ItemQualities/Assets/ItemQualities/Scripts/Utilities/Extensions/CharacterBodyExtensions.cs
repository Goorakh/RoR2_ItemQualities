using ItemQualities.Buffs;
using RoR2;
using System;
using System.Runtime.CompilerServices;

namespace ItemQualities.Utilities.Extensions
{
    public static class CharacterBodyExtensions
    {
        public static int GetBuffCountRaw(this CharacterBody body, BuffIndex buffIndex)
        {
            using (new BuffHooks.DisableBuffCountHooksScope(body))
            {
                return body.GetBuffCount(buffIndex);
            }
        }

        public static bool HasBuffRaw(this CharacterBody body, BuffIndex buffIndex)
        {
            using (new BuffHooks.DisableBuffCountHooksScope(body))
            {
                return body.HasBuff(buffIndex);
            }
        }

        public static void ClearTimedBuffsRaw(this CharacterBody body, BuffIndex buffIndex)
        {
            using (new BuffHooks.DisableBuffCountHooksScope(body))
            {
                body.ClearTimedBuffs(buffIndex);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BuffQualityCounts GetBuffCounts(this CharacterBody body, BuffQualityGroupIndex buffGroupIndex)
        {
            return body.GetBuffCounts(QualityCatalog.GetBuffQualityGroup(buffGroupIndex));
        }

        public static BuffQualityCounts GetBuffCounts(this CharacterBody body, BuffQualityGroup buffGroup)
        {
            if (!body)
                throw new ArgumentNullException(nameof(body));

            if (!buffGroup)
                return default;

            int baseCount = body.GetBuffCountRaw(buffGroup.BaseBuffIndex);
            int uncommonCount = body.GetBuffCountRaw(buffGroup.UncommonBuffIndex);
            int rareCount = body.GetBuffCountRaw(buffGroup.RareBuffIndex);
            int epicCount = body.GetBuffCountRaw(buffGroup.EpicBuffIndex);
            int legendaryCount = body.GetBuffCountRaw(buffGroup.LegendaryBuffIndex);

            return new BuffQualityCounts(baseCount, uncommonCount, rareCount, epicCount, legendaryCount);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RemoveAllBuffs(this CharacterBody body, BuffQualityGroupIndex buffGroupIndex)
        {
            body.RemoveAllBuffs(QualityCatalog.GetBuffQualityGroup(buffGroupIndex));
        }

        public static void RemoveAllBuffs(this CharacterBody body, BuffQualityGroup buffGroup)
        {
            if (!body)
                throw new ArgumentNullException(nameof(body));

            if (!buffGroup)
                return;

            for (QualityTier qualityTier = QualityTier.None; qualityTier < QualityTier.Count; qualityTier++)
            {
                BuffIndex buffIndex = buffGroup.GetBuffIndex(qualityTier);
                for (int i = body.GetBuffCountRaw(buffIndex); i > 0; i--)
                {
                    body.RemoveBuff(buffIndex);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RemoveAllQualityBuffs(this CharacterBody body, BuffQualityGroupIndex buffGroupIndex)
        {
            body.RemoveAllQualityBuffs(QualityCatalog.GetBuffQualityGroup(buffGroupIndex));
        }

        public static void RemoveAllQualityBuffs(this CharacterBody body, BuffQualityGroup buffGroup)
        {
            if (!body)
                throw new ArgumentNullException(nameof(body));

            if (!buffGroup)
                return;

            for (QualityTier qualityTier = 0; qualityTier < QualityTier.Count; qualityTier++)
            {
                BuffIndex buffIndex = buffGroup.GetBuffIndex(qualityTier);
                for (int i = body.GetBuffCountRaw(buffIndex); i > 0; i--)
                {
                    body.RemoveBuff(buffIndex);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertQualityBuffsToTier(this CharacterBody body, BuffQualityGroupIndex buffGroupIndex, QualityTier buffQualityTier)
        {
            body.ConvertQualityBuffsToTier(QualityCatalog.GetBuffQualityGroup(buffGroupIndex), buffQualityTier);
        }

        public static void ConvertQualityBuffsToTier(this CharacterBody body, BuffQualityGroup buffGroup, QualityTier buffQualityTier)
        {
            convertBuffs(body, buffGroup, buffQualityTier, false);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertAllBuffsToQualityTier(this CharacterBody body, BuffQualityGroupIndex buffGroupIndex, QualityTier buffQualityTier)
        {
            body.ConvertAllBuffsToQualityTier(QualityCatalog.GetBuffQualityGroup(buffGroupIndex), buffQualityTier);
        }

        public static void ConvertAllBuffsToQualityTier(this CharacterBody body, BuffQualityGroup buffGroup, QualityTier buffQualityTier)
        {
            convertBuffs(body, buffGroup, buffQualityTier, true);
        }

        static void convertBuffs(CharacterBody body, BuffQualityGroup buffGroup, QualityTier buffQualityTier, bool includeBaseBuff)
        {
            if (!body)
                throw new ArgumentNullException(nameof(body));

            if (!buffGroup)
                return;

            BuffIndex desiredBuffIndex = buffGroup.GetBuffIndex(buffQualityTier);
            BuffDef desiredBuffDef = BuffCatalog.GetBuffDef(desiredBuffIndex);

            for (QualityTier qualityTier = includeBaseBuff ? QualityTier.None : 0; qualityTier < QualityTier.Count; qualityTier++)
            {
                if (qualityTier != buffQualityTier)
                {
                    BuffIndex qualityBuffIndex = buffGroup.GetBuffIndex(qualityTier);
                    if (qualityBuffIndex != BuffIndex.None)
                    {
                        for (int i = body.GetBuffCountRaw(qualityBuffIndex); i > 0; i--)
                        {
                            float? buffDuration = null;
                            for (int j = 0; j < body.timedBuffs.Count; j++)
                            {
                                CharacterBody.TimedBuff timedBuff = body.timedBuffs[j];
                                if (timedBuff.buffIndex == qualityBuffIndex)
                                {
                                    buffDuration = timedBuff.timer;
                                    body.timedBuffs.RemoveAt(j);
                                    break;
                                }
                            }

                            body.RemoveBuff(qualityBuffIndex);

                            if (desiredBuffIndex != BuffIndex.None && desiredBuffDef && (desiredBuffDef.canStack || !body.HasBuffRaw(desiredBuffIndex)))
                            {
                                if (buffDuration.HasValue)
                                {
                                    body.AddTimedBuff(desiredBuffDef, buffDuration.Value);
                                }
                                else
                                {
                                    body.AddBuff(desiredBuffIndex);
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
