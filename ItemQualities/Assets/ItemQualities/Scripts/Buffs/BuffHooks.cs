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
        public delegate void BodyBuffGainedOrLostDelegate(CharacterBody body, BuffDef buffDef);
        public static event BodyBuffGainedOrLostDelegate OnBuffFirstStackGainedGlobal;
        public static event BodyBuffGainedOrLostDelegate OnBuffFinalStackLostGlobal;

        public delegate void BodyBuffCountChangedDelegate(CharacterBody body, BuffIndex buffIndex, int newCount);
        public static event BodyBuffCountChangedDelegate OnBodyBuffCountChangedGlobal;

        static readonly List<CharacterBody> _disableBuffCountHooksForBodies = new List<CharacterBody>();

        [SystemInitializer]
        static void Init()
        {
            On.RoR2.CharacterBody.SetBuffCount += On_CharacterBody_SetBuffCount;

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

            On.RoR2.CharacterBody.OnBuffFirstStackGained += CharacterBody_OnBuffFirstStackGained;
            On.RoR2.CharacterBody.OnBuffFinalStackLost += CharacterBody_OnBuffFinalStackLost;
        }

        static void On_CharacterBody_SetBuffCount(On.RoR2.CharacterBody.orig_SetBuffCount orig, CharacterBody self, BuffIndex buffType, int newCount)
        {
            orig(self, buffType, newCount);

            if (OnBodyBuffCountChangedGlobal != null)
            {
                try
                {
                    OnBodyBuffCountChangedGlobal(self, buffType, newCount);
                }
                catch (Exception e)
                {
                    Log.Warning_NoCallerPrefix(e);
                }
            }
        }

        static void CharacterBody_OnBuffFirstStackGained(On.RoR2.CharacterBody.orig_OnBuffFirstStackGained orig, CharacterBody self, BuffDef buffDef)
        {
            orig(self, buffDef);

            if (OnBuffFirstStackGainedGlobal != null)
            {
                foreach (BodyBuffGainedOrLostDelegate onBuffFirstStackGainedGlobal in OnBuffFirstStackGainedGlobal.GetInvocationList()
                                                                                                                  .OfType<BodyBuffGainedOrLostDelegate>())
                {
                    try
                    {
                        onBuffFirstStackGainedGlobal(self, buffDef);
                    }
                    catch (Exception e)
                    {
                        Log.Warning_NoCallerPrefix("Failed to invoke event listener for OnBuffFirstStackGained: " + e);
                    }
                }
            }
        }

        static void CharacterBody_OnBuffFinalStackLost(On.RoR2.CharacterBody.orig_OnBuffFinalStackLost orig, CharacterBody self, BuffDef buffDef)
        {
            orig(self, buffDef);

            if (OnBuffFinalStackLostGlobal != null)
            {
                foreach (BodyBuffGainedOrLostDelegate onBuffFinalStackLostGlobal in OnBuffFinalStackLostGlobal.GetInvocationList()
                                                                                                              .OfType<BodyBuffGainedOrLostDelegate>())
                {
                    try
                    {
                        onBuffFinalStackLostGlobal(self, buffDef);
                    }
                    catch (Exception e)
                    {
                        Log.Warning_NoCallerPrefix("Failed to invoke event listener for OnBuffFinalStackLost: " + e);
                    }
                }
            }
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
            ILCursor c = new ILCursor(il);

            static bool shouldInvokeBuffGainedOrLost(CharacterBody body, BuffDef buffDef)
            {
                BuffIndex buffIndex = buffDef ? buffDef.buffIndex : BuffIndex.None;
                BuffQualityGroupIndex buffGroupIndex = QualityCatalog.FindBuffQualityGroupIndex(buffIndex);

                if (buffGroupIndex != BuffQualityGroupIndex.Invalid)
                {
                    BuffQualityGroup buffGroup = QualityCatalog.GetBuffQualityGroup(buffGroupIndex);
                    if (!buffGroup.InheritBaseBuffBehavior)
                        return true;
                }

                // If this is a base buff and the body has any quality buff already, OnBuffFirstStackGained had already happened for this buff,
                // so we skip the call when it was actually added for real.
                return QualityCatalog.GetQualityTier(buffIndex) > QualityTier.None ||
                       buffGroupIndex == BuffQualityGroupIndex.Invalid ||
                       body.GetBuffCounts(buffGroupIndex).TotalQualityCount == 0;
            }

            static bool shouldInvokeBaseBuffGainedOrLost(CharacterBody body, BuffIndex buffIndex)
            {
                // If this is a quality buff, and it is the first of the group to be added to this body,
                // we should also invoke the OnBuffFirstStackGained for the base buff.

                QualityTier qualityTier = QualityCatalog.GetQualityTier(buffIndex);
                if (qualityTier == QualityTier.None)
                    return false;

                BuffQualityGroupIndex buffGroupIndex = QualityCatalog.FindBuffQualityGroupIndex(buffIndex);
                if (buffGroupIndex == BuffQualityGroupIndex.Invalid)
                    return false;

                BuffQualityGroup buffGroup = QualityCatalog.GetBuffQualityGroup(buffGroupIndex);
                if (!buffGroup.InheritBaseBuffBehavior || buffGroup.BaseBuffIndex == BuffIndex.None)
                    return false;

                for (QualityTier buffQualityTier = QualityTier.None; buffQualityTier < QualityTier.Count; buffQualityTier++)
                {
                    if (buffQualityTier != qualityTier)
                    {
                        BuffIndex qualityBuffIndex = buffGroup.GetBuffIndex(buffQualityTier);
                        if (qualityBuffIndex != BuffIndex.None && body.HasBuffRaw(qualityBuffIndex))
                        {
                            return false;
                        }
                    }
                }

                return true;
            }

            VariableDefinition buffDefVar = null;
            Instruction afterFinalBuffStackLostInstruction = null;
            if (c.TryGotoNext(MoveType.Before,
                              x => x.MatchLdarg(0),
                              x => x.MatchLdloc<BuffDef>(il, out buffDefVar),
                              x => x.MatchCallOrCallvirt<CharacterBody>(nameof(CharacterBody.OnBuffFinalStackLost)),
                              x => x.MatchAny(out afterFinalBuffStackLostInstruction)))
            {
                ILLabel afterFinalBuffStackLostLabel = c.DefineLabel();

                c.Emit(OpCodes.Ldarg_0);
                c.Emit(OpCodes.Ldloc, buffDefVar);
                c.EmitDelegate<Func<CharacterBody, BuffDef, bool>>(shouldInvokeBuffGainedOrLost);
                c.Emit(OpCodes.Brfalse, afterFinalBuffStackLostLabel);

                c.Goto(afterFinalBuffStackLostInstruction, MoveType.Before);
                c.MarkLabel(afterFinalBuffStackLostLabel);

                c.Emit(OpCodes.Ldarg_0);
                c.Emit(OpCodes.Ldloc, buffDefVar);
                c.EmitDelegate<Action<CharacterBody, BuffDef>>(tryInvokeOnFinalBaseBuffStackLost);

                static void tryInvokeOnFinalBaseBuffStackLost(CharacterBody body, BuffDef buffDef)
                {
                    BuffIndex buffIndex = buffDef ? buffDef.buffIndex : BuffIndex.None;
                    if (buffIndex != BuffIndex.None && shouldInvokeBaseBuffGainedOrLost(body, buffIndex))
                    {
                        BuffIndex baseBuffIndex = QualityCatalog.GetBuffIndexOfQuality(buffIndex, QualityTier.None);
                        if (baseBuffIndex != buffIndex)
                        {
                            body.OnBuffFinalStackLost(BuffCatalog.GetBuffDef(baseBuffIndex));
                        }
                    }
                }
            }
            else
            {
                Log.Error("Failed to find OnBuffFinalStackLost patch location");
            }

            c.Goto(0, MoveType.Before);

            Instruction afterFirstBuffStackGainedInstruction = null;
            if (c.TryGotoNext(MoveType.Before,
                              x => x.MatchLdarg(0),
                              x => x.MatchLdloc<BuffDef>(il, out buffDefVar),
                              x => x.MatchCallOrCallvirt<CharacterBody>(nameof(CharacterBody.OnBuffFirstStackGained)),
                              x => x.MatchAny(out afterFirstBuffStackGainedInstruction)))
            {
                ILLabel afterFirstBuffStackGainedLabel = c.DefineLabel();

                c.Emit(OpCodes.Ldarg_0);
                c.Emit(OpCodes.Ldloc, buffDefVar);
                c.EmitDelegate<Func<CharacterBody, BuffDef, bool>>(shouldInvokeBuffGainedOrLost);
                c.Emit(OpCodes.Brfalse, afterFirstBuffStackGainedLabel);

                c.Goto(afterFirstBuffStackGainedInstruction, MoveType.Before);
                c.MarkLabel(afterFirstBuffStackGainedLabel);

                c.Emit(OpCodes.Ldarg_0);
                c.Emit(OpCodes.Ldloc, buffDefVar);
                c.EmitDelegate<Action<CharacterBody, BuffDef>>(tryInvokeOnFirstBaseBuffStackGained);

                static void tryInvokeOnFirstBaseBuffStackGained(CharacterBody body, BuffDef buffDef)
                {
                    BuffIndex buffIndex = buffDef ? buffDef.buffIndex : BuffIndex.None;
                    if (buffIndex != BuffIndex.None && shouldInvokeBaseBuffGainedOrLost(body, buffIndex))
                    {
                        BuffIndex baseBuffIndex = QualityCatalog.GetBuffIndexOfQuality(buffIndex, QualityTier.None);
                        if (baseBuffIndex != buffIndex)
                        {
                            body.OnBuffFirstStackGained(BuffCatalog.GetBuffDef(baseBuffIndex));
                        }
                    }
                }
            }
            else
            {
                Log.Error("Failed to find OnBuffFirstStackGained patch location");
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
