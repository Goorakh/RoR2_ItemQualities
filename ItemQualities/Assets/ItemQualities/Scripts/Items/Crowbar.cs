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

        public static DelayedHitHandler HandleDelayedHit(GameObject attacker, GameObject victim)
        {
            DelayedHitHandler delayedHitHandler = victim.GetComponent<DelayedHitHandler>();
            if (!delayedHitHandler)
            {
                delayedHitHandler = victim.AddComponent<DelayedHitHandler>();
                delayedHitHandler.attacker = attacker;
            }
            return delayedHitHandler;
        }

        public static bool IsImmobile(EntityStateMachine entityStateMachine)
        {
            CharacterBody body = entityStateMachine.commonComponents.characterBody;
            if (entityStateMachine.state is StunState ||
                entityStateMachine.state is FrozenState ||
                entityStateMachine.state is ShockState ||
                entityStateMachine.state is ImmobilizeState ||
                entityStateMachine.state is GenericCharacterVehicleSeated ||
                entityStateMachine.state is ThrownObjectIdle ||
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
            CharacterBody attackerbody = report.attackerBody;
            if (!attackerbody)
            {
                return;
            }
            if (!delayedHitHandler) {
                //fallback if something immobilizes directly, set the proc owner to the first person attacking after that instead, this should handle immobilizing attack automatically
                //things that are procced or don't deal damage still need to be handled manually, like quality opal
                if (report.victimBody.TryGetComponent<EntityStateMachine>(out EntityStateMachine entityStateMachine) &&
                IsImmobile(entityStateMachine))
                {
                    delayedHitHandler = HandleDelayedHit(report.attacker, report.victimBody.gameObject);
                }
                else
                {
                    return;
                }
            }

            ItemQualityCounts crowbar = ItemQualitiesContent.ItemQualityGroups.Crowbar.GetItemCountsEffective(attackerbody.inventory);
            float multiplier =  crowbar.UncommonCount * 0.15f +
                                crowbar.RareCount * 0.3f +
                                crowbar.EpicCount * 0.45f +
                                crowbar.LegendaryCount * 0.6f;
   
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
                HandleDelayedHit(damageInfo.attacker, victim);
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
                HandleDelayedHit(damageReport.attacker, damageReport.victimBody.gameObject);
                return true;
            }
            return false;
        }

        public class DelayedHitHandler : MonoBehaviour
        {
            public float damage = 0;
            public GameObject attacker;

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
                HealthComponent healthComponent = GetComponent<HealthComponent>();
                if(!healthComponent || damage == 0)
                {
                    Destroy(this);
                    return;
                }

                if (!IsImmobile(_entityStateMachine))
                {
                    dealDelayedDamage(healthComponent);
                    Destroy(this);
                }
            }

            void dealDelayedDamage(HealthComponent healthComponent) {
                ProcChainMask procChainMask = default(ProcChainMask);
                procChainMask.AddModdedProc(ProcTypes.Immobilize);

                DamageInfo damageInfo = new DamageInfo
                {
                    damage = damage,
                    inflictor = attacker,
                    attacker = attacker,
                    procChainMask = procChainMask,
                    procCoefficient = 1,
                    damageColorIndex = DamageColorIndex.DelayedDamage,
                    damageType = DamageTypeExtended.BypassDamageCalculations,
                    position = _characterBody.corePosition,
                };
                healthComponent.TakeDamage(damageInfo);
                GlobalEventManager.instance.OnHitEnemy(damageInfo, healthComponent.gameObject);
                GlobalEventManager.instance.OnHitAll(damageInfo, healthComponent.gameObject);
            }
        }
    }
}
