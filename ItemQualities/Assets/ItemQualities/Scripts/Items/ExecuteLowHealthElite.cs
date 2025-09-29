using EntityStates;
using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using UnityEngine;

namespace ItemQualities.Items
{
    static class ExecuteLowHealthElite
    {
        [SystemInitializer]
        static void Init()
        {
            IL.RoR2.HealthComponent.TakeDamageProcess += HealthComponent_TakeDamageProcess;
        }

        static void HealthComponent_TakeDamageProcess(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (!il.Method.TryFindParameter<DamageInfo>(out ParameterDefinition damageInfoParameter))
            {
                Log.Error("Failed to find DamageInfo parameter");
                return;
            }

            if (!c.TryGotoNext(MoveType.Before,
                               x => x.MatchLdsfld<FrozenState>(nameof(FrozenState.executeEffectPrefab))))
            {
                Log.Error("Failed to find execute location");
                return;
            }

            int executeFractionLocalIndex = -1;
            if (!c.Clone().TryGotoPrev(x => x.MatchLdloc(typeof(float), il, out executeFractionLocalIndex)))
            {
                Log.Error("Failed to find executeFraction variable index");
                return;
            }

            int executeEffectPrefabLocalIndex = -1;
            if (!c.Clone().TryGotoNext(x => x.MatchStloc(typeof(GameObject), il, out executeEffectPrefabLocalIndex)))
            {
                Log.Error("Failed to find executeEffectPrefab variable index");
                return;
            }

            ILLabel afterFreezeExecuteLabel = default;
            if (!c.TryGotoPrev(x => x.MatchCallOrCallvirt<HealthComponent>("get_" + nameof(HealthComponent.isInFrozenState))) ||
                !c.TryGotoNext(x => x.MatchBrfalse(out afterFreezeExecuteLabel)))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Goto(afterFreezeExecuteLabel.Target, MoveType.AfterLabel);

            c.Emit(OpCodes.Ldarg_0);
            c.Emit(OpCodes.Ldarg, damageInfoParameter);
            c.Emit(OpCodes.Ldloca, executeFractionLocalIndex);
            c.Emit(OpCodes.Ldloca, executeEffectPrefabLocalIndex);
            c.EmitDelegate<HandleBossExecuteDelegate>(handleBossExecute);
        }

        delegate void HandleBossExecuteDelegate(HealthComponent healthComponent, DamageInfo damageInfo, ref float executeFraction, ref GameObject executeEffectPrefab);
        static void handleBossExecute(HealthComponent victim, DamageInfo damageInfo, ref float executeFraction, ref GameObject executeEffectPrefab)
        {
            if (!victim || !victim.body || damageInfo == null)
                return;

            CharacterBody attackerBody = damageInfo.attacker ? damageInfo.attacker.GetComponent<CharacterBody>() : null;

            if (attackerBody && attackerBody.TryGetComponent(out CharacterBodyExtraStatsTracker attackerBodyExtraStats))
            {
                if ((victim.body.isBoss || victim.body.isChampion) && executeFraction < attackerBodyExtraStats.ExecuteBossHealthFraction)
                {
                    executeFraction = attackerBodyExtraStats.ExecuteBossHealthFraction;
                    executeEffectPrefab = HealthComponent.AssetReferences.executeEffectPrefab;
                }
            }
        }
    }
}
