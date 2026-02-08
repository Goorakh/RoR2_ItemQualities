using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.Utils;
using RoR2;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ItemQualities.Buffs
{
    static class BuffHooks
    {
        static readonly List<CharacterBody> _disableBuffCountHooksForBodies = new List<CharacterBody>();

        [SystemInitializer]
        static void Init()
        {
            On.RoR2.CharacterBody.GetBuffCount_BuffIndex += CharacterBody_GetBuffCount_BuffIndex;
            On.RoR2.CharacterBody.ClearTimedBuffs_BuffIndex += CharacterBody_ClearTimedBuffs_BuffIndex;
            On.RoR2.CharacterBody.RemoveBuff_BuffIndex += CharacterBody_RemoveBuff_BuffIndex;
            On.RoR2.CharacterBody.ClearAllBuffs += CharacterBody_ClearAllBuffs;

            On.RoR2.UI.BuffDisplay.AllocateIcons += BuffDisplay_AllocateIcons;

            IL.RoR2.CharacterBody.SetBuffCount += CharacterBody_SetBuffCount;

            IL.RoR2.CharacterBody.RemoveBuff_BuffIndex += patchBuffEqualityComparison;
            IL.RoR2.CharacterBody.OnBuffFinalStackLost += patchBuffEqualityComparison;
            IL.RoR2.CharacterBody.AddOrRemoveEliteItemBehavior += patchBuffEqualityComparison;
            IL.RoR2.CharacterBody.OnBuffFirstStackGained += patchBuffEqualityComparison;
        }

        static int CharacterBody_GetBuffCount_BuffIndex(On.RoR2.CharacterBody.orig_GetBuffCount_BuffIndex orig, CharacterBody self, BuffIndex buffType)
        {
            int buffCount = orig(self, buffType);

            if (!_disableBuffCountHooksForBodies.Contains(self))
            {
                try
                {
                    if (QualityCatalog.GetQualityTier(buffType) == QualityTier.None)
                    {
                        BuffQualityGroupIndex buffGroupIndex = QualityCatalog.FindBuffQualityGroupIndex(buffType);
                        if (buffGroupIndex != BuffQualityGroupIndex.Invalid)
                        {
                            BuffQualityGroup buffGroup = QualityCatalog.GetBuffQualityGroup(buffGroupIndex);
                            if (buffGroup.InheritBaseBuffBehavior)
                            {
                                for (QualityTier qualityTier = 0; qualityTier < QualityTier.Count; qualityTier++)
                                {
                                    BuffIndex qualityBuffIndex = buffGroup.GetBuffIndex(qualityTier);
                                    if (qualityBuffIndex != BuffIndex.None)
                                    {
                                        buffCount += self.GetBuffCountRaw(qualityBuffIndex);
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Log.Warning_NoCallerPrefix(e);
                }
            }

            return buffCount;
        }

        static void CharacterBody_ClearTimedBuffs_BuffIndex(On.RoR2.CharacterBody.orig_ClearTimedBuffs_BuffIndex orig, CharacterBody self, BuffIndex buffType)
        {
            orig(self, buffType);

            if (_disableBuffCountHooksForBodies.Contains(self))
                return;

            try
            {
                if (QualityCatalog.GetQualityTier(buffType) == QualityTier.None)
                {
                    BuffQualityGroupIndex buffGroupIndex = QualityCatalog.FindBuffQualityGroupIndex(buffType);
                    if (buffGroupIndex != BuffQualityGroupIndex.Invalid)
                    {
                        BuffQualityGroup buffGroup = QualityCatalog.GetBuffQualityGroup(buffGroupIndex);
                        if (buffGroup.InheritBaseBuffBehavior)
                        {
                            for (QualityTier qualityTier = 0; qualityTier < QualityTier.Count; qualityTier++)
                            {
                                BuffIndex qualityBuffIndex = buffGroup.GetBuffIndex(qualityTier);
                                if (qualityBuffIndex != BuffIndex.None)
                                {
                                    self.ClearTimedBuffs(qualityBuffIndex);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warning_NoCallerPrefix(e);
            }
        }

        static void CharacterBody_RemoveBuff_BuffIndex(On.RoR2.CharacterBody.orig_RemoveBuff_BuffIndex orig, CharacterBody self, BuffIndex buffType)
        {
            if (!_disableBuffCountHooksForBodies.Contains(self))
            {
                try
                {
                    if (self.GetBuffCountRaw(buffType) == 0 && QualityCatalog.GetQualityTier(buffType) == QualityTier.None)
                    {
                        BuffQualityGroupIndex buffGroupIndex = QualityCatalog.FindBuffQualityGroupIndex(buffType);
                        if (buffGroupIndex != BuffQualityGroupIndex.Invalid)
                        {
                            BuffQualityGroup buffGroup = QualityCatalog.GetBuffQualityGroup(buffGroupIndex);
                            if (buffGroup.InheritBaseBuffBehavior)
                            {
                                for (QualityTier qualityTier = 0; qualityTier < QualityTier.Count; qualityTier++)
                                {
                                    BuffIndex qualityBuffIndex = buffGroup.GetBuffIndex(qualityTier);
                                    if (qualityBuffIndex != BuffIndex.None && self.GetBuffCountRaw(qualityBuffIndex) > 0)
                                    {
                                        buffType = qualityBuffIndex;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Log.Warning_NoCallerPrefix(e);
                }
            }

            orig(self, buffType);
        }

        static void CharacterBody_ClearAllBuffs(On.RoR2.CharacterBody.orig_ClearAllBuffs orig, CharacterBody self, BuffDef buffToSet)
        {
            if (_disableBuffCountHooksForBodies.Contains(self))
            {
                orig(self, buffToSet);
                return;
            }

            try
            {
                BuffIndex buffIndex = buffToSet ? buffToSet.buffIndex : BuffIndex.None;

                if (QualityCatalog.GetQualityTier(buffIndex) == QualityTier.None)
                {
                    BuffQualityGroupIndex buffGroupIndex = QualityCatalog.FindBuffQualityGroupIndex(buffIndex);
                    if (buffGroupIndex != BuffQualityGroupIndex.Invalid)
                    {
                        BuffQualityGroup buffGroup = QualityCatalog.GetBuffQualityGroup(buffGroupIndex);
                        if (buffGroup.InheritBaseBuffBehavior)
                        {
                            for (QualityTier qualityTier = 0; qualityTier < QualityTier.Count; qualityTier++)
                            {
                                BuffIndex qualityBuffIndex = buffGroup.GetBuffIndex(qualityTier);
                                if (qualityBuffIndex != BuffIndex.None && self.GetBuffCountRaw(qualityBuffIndex) > 0)
                                {
                                    using (new DisableBuffCountHooksScope(self))
                                    {
                                        self.ClearAllBuffs(BuffCatalog.GetBuffDef(qualityBuffIndex));
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warning_NoCallerPrefix(e);
            }

            using (new DisableBuffCountHooksScope(self))
            {
                orig(self, buffToSet);
            }
        }

        static void BuffDisplay_AllocateIcons(On.RoR2.UI.BuffDisplay.orig_AllocateIcons orig, RoR2.UI.BuffDisplay self)
        {
            using (new DisableBuffCountHooksScope(self.source))
            {
                orig(self);
            }
        }

        static void CharacterBody_SetBuffCount(ILContext il)
        {
            if (!il.Method.TryFindParameter<BuffIndex>(out ParameterDefinition buffIndexParameter))
            {
                Log.Error("Failed to find BuffIndex parameter");
                return;
            }

            ILCursor c = new ILCursor(il);

            // IL_001C: ldloc.0
            // IL_001D: ldind.i4
            // IL_001E: stloc.1
            // int oldCount = buffCountRef;

            // IL_001F: ldloc.0
            // IL_0020: ldarg.2
            // IL_0021: stind.i4
            // buffCountRef = newCount;

            VariableDefinition buffCountRefVar = null;
            VariableDefinition oldCountVar = null;
            ParameterDefinition newCountParameter = null;
            if (!c.TryGotoNext(MoveType.After,
                               x => x.MatchLdloc(il, out buffCountRefVar),
                               x => x.MatchLdindI4(),
                               x => x.MatchStloc<int>(il, out oldCountVar),

                               x => x.MatchLdloc(buffCountRefVar),
                               x => x.MatchLdarg<int>(il, out newCountParameter),
                               x => x.MatchStindI4()))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Emit(OpCodes.Ldarg_0);
            c.Emit(OpCodes.Ldarg, buffIndexParameter);
            c.EmitDelegate<Func<CharacterBody, BuffIndex, int>>(calculateBonusBuffCount);

            c.Emit(OpCodes.Dup);

            c.Emit(OpCodes.Ldloc, oldCountVar);
            c.Emit(OpCodes.Add);
            c.Emit(OpCodes.Stloc, oldCountVar);

            c.Emit(OpCodes.Ldarg, newCountParameter);
            c.Emit(OpCodes.Add);
            c.Emit(OpCodes.Starg, newCountParameter);

            static int calculateBonusBuffCount(CharacterBody self, BuffIndex buffIndex)
            {
                int bonusBuffCount = 0;

                BuffQualityGroupIndex buffGroupIndex = QualityCatalog.FindBuffQualityGroupIndex(buffIndex);
                if (buffGroupIndex != BuffQualityGroupIndex.Invalid)
                {
                    BuffQualityGroup buffGroup = QualityCatalog.GetBuffQualityGroup(buffGroupIndex);
                    if (buffGroup.InheritBaseBuffBehavior)
                    {
                        for (QualityTier qualityTier = QualityTier.None; qualityTier < QualityTier.Count; qualityTier++)
                        {
                            BuffIndex qualityBuffIndex = buffGroup.GetBuffIndex(qualityTier);
                            if (qualityBuffIndex != BuffIndex.None && qualityBuffIndex != buffIndex)
                            {
                                bonusBuffCount += self.GetBuffCountRaw(qualityBuffIndex);
                            }
                        }
                    }
                }

                return bonusBuffCount;
            }
        }

        static void patchBuffEqualityComparison(ILContext il)
        {
            bool anyPatchAttempted = false;
            bool anyPatchSucceeded = false;

            ParameterDefinition buffDefParameter = il.Method.Parameters.FirstOrDefault(p => p.ParameterType.Is(typeof(BuffDef)));
            if (buffDefParameter != null)
            {
                ILCursor c = new ILCursor(il);

                int patchCount = 0;

                FieldReference buffField = null;
                Instruction buffMatchInstruction = null;
                while (c.TryGotoNext(MoveType.AfterLabel,
                                     x => x.MatchLdarg(buffDefParameter),
                                     x => x.MatchLdsfld(out buffField),
                                     x => x.MatchOpEquality<UnityEngine.Object>(),
                                     x => x.MatchBrfalse(out _),
                                     x => x.MatchAny(out buffMatchInstruction)))
                {
                    ILLabel buffMatchLabel = c.DefineLabel();

                    c.Emit(OpCodes.Ldarg, buffDefParameter);
                    c.Emit(OpCodes.Ldsfld, buffField);
                    c.EmitDelegate<Func<BuffDef, BuffDef, bool>>(matchBuffsWithQuality);
                    c.Emit(OpCodes.Brtrue, buffMatchLabel);

                    static bool matchBuffsWithQuality(BuffDef buffA, BuffDef buffB)
                    {
                        if (buffA == buffB)
                            return false; // If they are exactly equal, let the original comparison handle it still

                        BuffIndex buffIndexA = buffA ? buffA.buffIndex : BuffIndex.None;
                        BuffIndex buffIndexB = buffB ? buffB.buffIndex : BuffIndex.None;

                        BuffQualityGroupIndex buffGroupIndexA = QualityCatalog.FindBuffQualityGroupIndex(buffIndexA);
                        if (buffGroupIndexA == BuffQualityGroupIndex.Invalid)
                            return false;

                        BuffQualityGroup buffGroupA = QualityCatalog.GetBuffQualityGroup(buffGroupIndexA);
                        if (!buffGroupA.InheritBaseBuffBehavior)
                            return false;

                        BuffQualityGroupIndex buffGroupIndexB = QualityCatalog.FindBuffQualityGroupIndex(buffIndexB);

                        return buffGroupIndexA == buffGroupIndexB;
                    }

                    c.Goto(buffMatchInstruction, MoveType.Before);
                    c.MarkLabel(buffMatchLabel);

                    patchCount++;
                }

                Log.Debug($"{il.Method.FullName}: Found {patchCount} BuffDef patch location(s)");

                anyPatchAttempted = true;

                if (patchCount != 0)
                {
                    anyPatchSucceeded = true;
                }
            }

            ParameterDefinition buffIndexParameter = il.Method.Parameters.FirstOrDefault(p => p.ParameterType.Is(typeof(BuffIndex)));
            if (buffIndexParameter != null)
            {
                ILCursor c = new ILCursor(il);

                int patchCount = 0;

                FieldReference buffDefField = null;
                Instruction buffMatchInstruction = null;
                while (c.TryGotoNext(MoveType.AfterLabel,
                                     x => x.MatchLdarg(buffIndexParameter),
                                     x => x.MatchLdsfld(out buffDefField),
                                     x => x.MatchCallOrCallvirt<BuffDef>("get_" + nameof(BuffDef.buffIndex)),
                                     x => x.MatchBneUn(out _),
                                     x => x.MatchAny(out buffMatchInstruction)))
                {
                    ILLabel buffMatchLabel = c.DefineLabel();

                    c.Emit(OpCodes.Ldarg, buffIndexParameter);
                    c.Emit(OpCodes.Ldsfld, buffDefField);
                    c.EmitDelegate<Func<BuffIndex, BuffDef, bool>>(matchBuffsWithQuality);
                    c.Emit(OpCodes.Brtrue, buffMatchLabel);

                    static bool matchBuffsWithQuality(BuffIndex buffIndexA, BuffDef buffB)
                    {
                        BuffIndex buffIndexB = buffB ? buffB.buffIndex : BuffIndex.None;

                        if (buffIndexA == buffIndexB)
                            return false; // If they are exactly equal, let the original comparison handle it still

                        BuffQualityGroupIndex buffGroupIndexA = QualityCatalog.FindBuffQualityGroupIndex(buffIndexA);
                        if (buffGroupIndexA == BuffQualityGroupIndex.Invalid)
                            return false;

                        BuffQualityGroup buffGroupA = QualityCatalog.GetBuffQualityGroup(buffGroupIndexA);
                        if (!buffGroupA.InheritBaseBuffBehavior)
                            return false;

                        BuffQualityGroupIndex buffGroupIndexB = QualityCatalog.FindBuffQualityGroupIndex(buffIndexB);

                        return buffGroupIndexA == buffGroupIndexB;
                    }

                    c.Goto(buffMatchInstruction, MoveType.Before);
                    c.MarkLabel(buffMatchLabel);

                    patchCount++;
                }

                Log.Debug($"{il.Method.FullName}: Found {patchCount} BuffIndex patch location(s)");

                anyPatchAttempted = true;

                if (patchCount != 0)
                {
                    anyPatchSucceeded = true;
                }
            }

            if (!anyPatchAttempted)
            {
                Log.Error($"{il.Method.FullName}: Method is not valid for patch");
            }
            else if (!anyPatchSucceeded)
            {
                Log.Error($"{il.Method.FullName}: Failed to find any patch location");
            }
        }

        public readonly ref struct DisableBuffCountHooksScope
        {
            readonly CharacterBody _body;

            public DisableBuffCountHooksScope(CharacterBody body)
            {
                _body = body;
                _disableBuffCountHooksForBodies.Add(_body);
            }

            public readonly void Dispose()
            {
                _disableBuffCountHooksForBodies.Remove(_body);
            }
        }
    }
}
