using EntityStates;
using EntityStates.Vehicles;
using ItemQualities.Utilities.Extensions;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using R2API;
using RoR2;
using System;
using UnityEngine;

namespace ItemQualities.Items
{
    static class Crowbar
    {
        [SystemInitializer]
        static void Init()
        {
            IL.RoR2.SetStateOnHurt.OnTakeDamageServer += SetStateOnHurt_OnTakeDamageServer;
            IL.RoR2.GlobalEventManager.ProcessHitEnemy += GlobalEventManager_ProcessHitEnemy;
            GlobalEventManager.onServerDamageDealt += onServerDamageDealt;
        }

        public static DelayedHitHandler handleDelayedHit(DamageInfo damageInfo, GameObject victim)
        {
            DelayedHitHandler delayedHitHandler = victim.GetComponent<DelayedHitHandler>();
            if (!delayedHitHandler)
            {
                damageInfo.procChainMask.AddModdedProc(ProcTypes.Immobilize);
                delayedHitHandler = victim.AddComponent<DelayedHitHandler>();
                delayedHitHandler.damageInfo = damageInfo;
            }
            return delayedHitHandler;
        }

        public static DelayedHitHandler handleDelayedHit(GameObject attacker, GameObject victim)
        {
            DamageInfo damageInfo = new DamageInfo
            {
                attacker = attacker,
                inflictor = attacker,
                procChainMask = new ProcChainMask()
            };

            return handleDelayedHit(damageInfo, victim);
        }

        public static bool is_immobile(EntityState state, CharacterBody body)
        {
            if (state.GetType() == typeof(StunState) ||
                state.GetType() == typeof(FrozenState) ||
                state.GetType() == typeof(ShockState) ||
                state.GetType() == typeof(HurtState) ||
                state.GetType() == typeof(ImmobilizeState) ||
                state.GetType() == typeof(GenericCharacterVehicleSeated) ||
                state.GetType() == typeof(ThrownObjectIdle) ||
                body.HasBuff(RoR2Content.Buffs.Nullified) ||
                body.HasBuff(RoR2Content.Buffs.Entangle))
            {
                return true;
            }
            return false;
        }

        private static void onServerDamageDealt(DamageReport report)
        {
            DelayedHitHandler delayedHitHandler = report.victimBody.GetComponent<DelayedHitHandler>();
            CharacterBody attckerbody = report.attackerBody;
            if(!delayedHitHandler) {
                //fallback if something stuns directly, set the proc owner to the first person attacking after that instead, this should handle stunning attack automatically
                //things that are procced or don't deal damage still need to be handled manually, like quality opal
                if (report.victimBody.TryGetComponent<EntityStateMachine>(out EntityStateMachine entityStateMachine) &&
                is_immobile(entityStateMachine.state, report.victimBody))
                {
                    delayedHitHandler = handleDelayedHit(report.damageInfo, report.victimBody.gameObject);
                }
                else
                {
                    return;
                }
            }
            if (!attckerbody) 
            {
                return;
            }

            ItemQualityCounts crowbar = ItemQualitiesContent.ItemQualityGroups.Crowbar.GetItemCountsEffective(attckerbody.inventory);
            float multiplier =  crowbar.UncommonCount * 0.1f +
                                crowbar.RareCount * 0.2f +
                                crowbar.EpicCount * 0.3f +
                                crowbar.LegendaryCount * 0.4f;

            delayedHitHandler.damage += report.damageDealt * multiplier;
        }

        private static void GlobalEventManager_ProcessHitEnemy(ILContext il)
        {
            ILLabel label = null;
            ILCursor c = new ILCursor(il);
            //tentabauble
            if (!c.TryGotoNext(MoveType.After,
                    x => x.MatchCall(typeof(Util), "ConvertAmplificationPercentageIntoReductionPercentage"),
                    x => x.MatchLdloc(9),
                    x => x.MatchLdloca(4),
                    x => x.MatchCall(typeof(GlobalEventManager), "<ProcessHitEnemy>g__LocalCheckRoll|10_0"),
                    x => x.MatchBrfalse(out label)
                ))
            {
                Log.Error(il.Method.Name + " IL Hook failed!");
                return;
            }
            c.Emit(OpCodes.Ldarg_1);
            c.Emit(OpCodes.Ldarg_2);
            c.EmitDelegate<Func<DamageInfo, GameObject, bool>>(checkImmobileProcChainMask);
            c.Emit(OpCodes.Brfalse_S, label);
        }

        private static bool checkImmobileProcChainMask(DamageInfo damageInfo, GameObject victim)
        {
            if (!damageInfo.procChainMask.HasModdedProc(ProcTypes.Immobilize))
            {
                handleDelayedHit(damageInfo, victim);
                return true;
            }
            return false;
        }

        private static void SetStateOnHurt_OnTakeDamageServer(ILContext il)
        {
            ILLabel label = null;
            ILCursor c = new ILCursor(il);
            //stungrenade
            if (!c.TryGotoNext(MoveType.After,
                    x => x.MatchCall(typeof(Util), "CheckRoll"),
                    x => x.MatchBrfalse(out label)
                ))
            {
                Log.Error(il.Method.Name + " IL Hook failed!");
                return;
            }
            c.Emit(OpCodes.Ldarg_1);
            c.EmitDelegate<Func<DamageReport, bool>>(checkImmobileProcChainMask);
            c.Emit(OpCodes.Brfalse_S, label);

            //freezeonhit
            if (!c.TryGotoNext(MoveType.After,
                    x => x.MatchLdcI4(256),
            x => x.MatchCall(typeof(DamageTypeCombo), "op_Implicit"),
                    x => x.MatchCall(typeof(DamageTypeCombo), "op_BitwiseAnd"),
                    x => x.MatchCall(typeof(DamageTypeCombo), "op_Implicit"),
                    x => x.MatchBrfalse(out label)
                ))
            {
                Log.Error(il.Method.Name + " IL Hook failed!");
                return;
            }
            c.Emit(OpCodes.Ldarg_1);
            c.EmitDelegate<Func<DamageReport, bool>>(checkImmobileProcChainMask);
            c.Emit(OpCodes.Brfalse_S, label);

            //shockonhit
            if (!c.TryGotoNext(MoveType.After,
                    x => x.MatchLdcI4(16777216),
                    x => x.MatchCall(typeof(DamageTypeCombo), "op_Implicit"),
                    x => x.MatchCall(typeof(DamageTypeCombo), "op_BitwiseAnd"),
                    x => x.MatchCall(typeof(DamageTypeCombo), "op_Implicit"),
                    x => x.MatchBrfalse(out label)
                ))
            {
                Log.Error(il.Method.Name + " IL Hook failed!");
                return;
            }
            c.Emit(OpCodes.Ldarg_1);
            c.EmitDelegate<Func<DamageReport, bool>>(checkImmobileProcChainMask);
            c.Emit(OpCodes.Brfalse_S, label);

            //stagger
            if (!c.TryGotoNext(MoveType.After,
                    x => x.MatchLdcI4(32),
                    x => x.MatchCall(typeof(DamageTypeCombo), "op_Implicit"),
                    x => x.MatchCall(typeof(DamageTypeCombo), "op_BitwiseAnd"),
                    x => x.MatchCall(typeof(DamageTypeCombo), "op_Implicit"),
                    x => x.MatchBrfalse(out label)
                ))
            {
                Log.Error(il.Method.Name + " IL Hook failed!");
                return;
            }
            c.Emit(OpCodes.Ldarg_1);
            c.EmitDelegate<Func<DamageReport, bool>>(checkImmobileProcChainMask);
            c.Emit(OpCodes.Brfalse_S, label);

            //immobilizestate
            if (!c.TryGotoNext(MoveType.After,
                    x => x.MatchCall(typeof(SetStateOnHurt), "GetShouldHitStun"),
                    x => x.MatchBrfalse(out label)
                ))
            {
                Log.Error(il.Method.Name + " IL Hook failed!");
                return;
            }
            c.Emit(OpCodes.Ldarg_1);
            c.EmitDelegate<Func<DamageReport, bool>>(checkImmobileProcChainMask);
            c.Emit(OpCodes.Brfalse_S, label);

            if (!c.TryGotoNext(MoveType.After,
                    x => x.MatchLdcI4(1024),
                    x => x.MatchAnd(),
                    x => x.MatchBrfalse(out label)
                ))
            {
                Log.Error(il.Method.Name + " IL Hook failed!");
                return;
            }
            c.Emit(OpCodes.Ldarg_1);
            c.EmitDelegate<Func<DamageReport, bool>>(checkImmobileProcChainMask);
            c.Emit(OpCodes.Brfalse_S, label);
        }

        private static bool checkImmobileProcChainMask(DamageReport damageReport)
        {
            if (!damageReport.damageInfo.procChainMask.HasModdedProc(ProcTypes.Immobilize))
            {
                handleDelayedHit(damageReport.damageInfo, damageReport.victimBody.gameObject);
                return true;
            }
            return false;
        }

        public class DelayedHitHandler : MonoBehaviour
        {
            public float damage = 0;
            public DamageInfo damageInfo;

            EntityStateMachine _entityStateMachine;
            CharacterBody _characterBody;

            private void Awake()
            {
                _entityStateMachine = GetComponent<EntityStateMachine>();
                _characterBody = GetComponent<CharacterBody>();
                if(!_entityStateMachine || !_characterBody)
                {
                    Destroy(this);
                }
            }

            private void FixedUpdate()
            {
                if(is_immobile(_entityStateMachine.state, _characterBody)) {
                    return;
                }

                HealthComponent healthComponent = GetComponent<HealthComponent>();
                if(!healthComponent || damage == 0)
                {
                    Destroy(this);
                    return;
                }
                
                damageInfo = new DamageInfo
                {
                    damage = damage,
                    inflictor = damageInfo.inflictor,
                    attacker = damageInfo.attacker,
                    procChainMask = damageInfo.procChainMask,
                    crit = damageInfo.crit,
                    procCoefficient = 1,
                    position = damageInfo.position,
                    inflictedHurtbox = damageInfo.inflictedHurtbox,
                    damageColorIndex = DamageColorIndex.DelayedDamage,
                };
                healthComponent.TakeDamage(damageInfo);
                GlobalEventManager.instance.OnHitEnemy(damageInfo, healthComponent.gameObject);
                GlobalEventManager.instance.OnHitAll(damageInfo, healthComponent.gameObject);
                Destroy(this);
            }
        }
    }
}
