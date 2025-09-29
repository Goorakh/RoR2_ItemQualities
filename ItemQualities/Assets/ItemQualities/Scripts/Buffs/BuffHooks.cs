using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.Utils;
using RoR2;
using System;

namespace ItemQualities.Buffs
{
    static class BuffHooks
    {
        [SystemInitializer]
        static void Init()
        {
            IL.RoR2.CharacterBody.UpdateAllTemporaryVisualEffects += CombineGroupedBuffCountsPatch;
        }

        public static void CombineGroupedBuffCountsPatch(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            VariableDefinition bodyTempVar = il.AddVariable<CharacterBody>();
            VariableDefinition buffIndexTempVar = il.AddVariable<BuffIndex>();
            VariableDefinition buffDefTempVar = il.AddVariable<BuffDef>();

            static int tryGetCombinedBuffCountShared(int buffCount, CharacterBody body, BuffIndex buffIndex)
            {
                if (body)
                {
                    BuffQualityGroup buffGroup = QualityCatalog.GetBuffQualityGroup(QualityCatalog.FindBuffQualityGroupIndex(buffIndex));
                    if (buffGroup)
                    {
                        buffCount = buffGroup.GetBuffCounts(body).TotalCount;
                    }
                }

                return buffCount;
            }

            int patchCount = 0;

            while (c.TryGotoNext(MoveType.Before,
                                 x => x.MatchCallOrCallvirt<CharacterBody>(nameof(CharacterBody.GetBuffCount))))
            {
                bool isBuffIndex = ((MethodReference)c.Next.Operand).Parameters[0].ParameterType.Is(typeof(BuffIndex));

                VariableDefinition buffArgTempVar = isBuffIndex ? buffIndexTempVar : buffDefTempVar;

                c.EmitStoreStack(bodyTempVar, buffArgTempVar);

                c.Index++;

                c.Emit(OpCodes.Ldloc, bodyTempVar);
                c.Emit(OpCodes.Ldloc, buffArgTempVar);

                if (isBuffIndex)
                {
                    c.EmitDelegate<Func<int, CharacterBody, BuffIndex, int>>(tryGetCombinedBuffCount);
                    static int tryGetCombinedBuffCount(int baseBuffCount, CharacterBody body, BuffIndex buffIndex)
                    {
                        return tryGetCombinedBuffCountShared(baseBuffCount, body, buffIndex);
                    }
                }
                else
                {
                    c.EmitDelegate<Func<int, CharacterBody, BuffDef, int>>(tryGetCombinedBuffCount);
                    static int tryGetCombinedBuffCount(int baseBuffCount, CharacterBody body, BuffDef buffDef)
                    {
                        return tryGetCombinedBuffCountShared(baseBuffCount, body, buffDef ? buffDef.buffIndex : BuffIndex.None);
                    }
                }

                patchCount++;
            }

            c.Index = 0;
            while (c.TryGotoNext(MoveType.Before,
                                 x => x.MatchCallOrCallvirt<CharacterBody>(nameof(CharacterBody.HasBuff))))
            {
                bool isBuffIndex = ((MethodReference)c.Next.Operand).Parameters[0].ParameterType.Is(typeof(BuffIndex));

                VariableDefinition buffArgTempVar = isBuffIndex ? buffIndexTempVar : buffDefTempVar;

                c.EmitStoreStack(bodyTempVar, buffArgTempVar);

                c.Index++;

                c.Emit(OpCodes.Ldloc, bodyTempVar);
                c.Emit(OpCodes.Ldloc, buffArgTempVar);

                if (isBuffIndex)
                {
                    c.EmitDelegate<Func<bool, CharacterBody, BuffIndex, bool>>(tryGetCombinedHasBuff);
                    static bool tryGetCombinedHasBuff(bool hasBaseBuff, CharacterBody body, BuffIndex buffIndex)
                    {
                        return tryGetCombinedBuffCountShared(hasBaseBuff ? 1 : 0, body, buffIndex) > 0;
                    }
                }
                else
                {
                    c.EmitDelegate<Func<bool, CharacterBody, BuffDef, bool>>(tryGetCombinedHasBuff);
                    static bool tryGetCombinedHasBuff(bool hasBaseBuff, CharacterBody body, BuffDef buffDef)
                    {
                        return tryGetCombinedBuffCountShared(hasBaseBuff ? 1 : 0, body, buffDef ? buffDef.buffIndex : BuffIndex.None) > 0;
                    }
                }

                patchCount++;
            }

            if (patchCount == 0)
            {
                Log.Error($"Failed to find patch location for {il.Method.FullName}");
            }
            else
            {
                Log.Debug($"Found {patchCount} patch location(s) for {il.Method.FullName}");
            }
        }
    }
}
