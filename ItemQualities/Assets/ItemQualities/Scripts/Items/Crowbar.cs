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
            if (!c.TryGotoNext(MoveType.Before,
                    x => x.MatchBrfalse(out label),
                    x => x.MatchLdloc(out _),
                    x => x.MatchLdsfld(typeof(RoR2Content.Buffs), nameof(RoR2Content.Buffs.Nullified)),
                    x => x.MatchLdcR4(1),
                    x => x.MatchLdloc(out _),
                    x => x.MatchConvR4(),
                    x => x.MatchMul(),
                    x => x.MatchCallOrCallvirt(typeof(CharacterBody), nameof(CharacterBody.AddTimedBuff))
                ))
            {
                Log.Error(il.Method.Name + " IL Hook failed!");
                return;
            }
            c.Index++;
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
            if (c.TryGotoNext(MoveType.Before,
                    x => x.MatchBrfalse(out label),
                    x => x.MatchLdstr("Prefabs/Effects/ImpactEffects/ImpactStunGrenade"),
                    x => x.MatchCallOrCallvirt(typeof(LegacyResourcesAPI), nameof(LegacyResourcesAPI.Load)),
                    x => x.MatchLdloc(out _),
                    x => x.MatchLdfld(typeof(DamageInfo), nameof(DamageInfo.position)),
                    x => x.MatchLdloc(out _),
                    x => x.MatchLdfld(typeof(DamageInfo), nameof(DamageInfo.force)),
                    x => x.MatchCallOrCallvirt(typeof(Vector3), "op_UnaryNegation"),
                    x => x.MatchLdcI4(1),
                    x => x.MatchCallOrCallvirt(typeof(EffectManager), nameof(EffectManager.SimpleImpactEffect)),
                    x => x.MatchLdarg(0),
                    x => x.MatchLdcR4(2),
                    x => x.MatchCallOrCallvirt(typeof(SetStateOnHurt), nameof(SetStateOnHurt.SetStun))
                ))
            {
                c.Index++;
                c.Emit(OpCodes.Ldarg_1);
                c.EmitDelegate<Func<DamageReport, bool>>(checkImmobileProcChainMask);
                c.Emit(OpCodes.Brfalse, label);
            } else {
                Log.Error("stungrenade IL Hook failed!");
            }

            //freezeonhit
            c.Index = 0;
            if (c.TryGotoNext(MoveType.Before,
                    x => x.MatchBrfalse(out label),
                    x => x.MatchLdarg(0),
                    x => x.MatchLdcR4(2),
                    x => x.MatchLdloc(out _),
                    x => x.MatchLdfld(typeof(DamageInfo), nameof(DamageInfo.procCoefficient)),
                    x => x.MatchMul(),
                    x => x.MatchCallOrCallvirt(typeof(SetStateOnHurt), nameof(SetStateOnHurt.SetFrozen))
                ))
            {
                c.Index++;
                c.Emit(OpCodes.Ldarg_1);
                c.EmitDelegate<Func<DamageReport, bool>>(checkImmobileProcChainMask);
                c.Emit(OpCodes.Brfalse, label);
            } else { 
                Log.Error("freezeonhit IL Hook failed!");
            }

            //shockonhit
            c.Index = 0;
            if (c.TryGotoNext(MoveType.Before,
                    x => x.MatchBrfalse(out label),
                    x => x.MatchLdarg(0),
                    x => x.MatchLdcR4(5),
                    x => x.MatchLdarg(1),
                    x => x.MatchLdfld(typeof(DamageReport), nameof(DamageReport.damageInfo)),
                    x => x.MatchLdfld(typeof(DamageInfo), nameof(DamageInfo.procCoefficient)),
                    x => x.MatchMul(),
                    x => x.MatchCallOrCallvirt(typeof(SetStateOnHurt), nameof(SetStateOnHurt.SetShock))
                ))
            {
                c.Index++;
                c.Emit(OpCodes.Ldarg_1);
                c.EmitDelegate<Func<DamageReport, bool>>(checkImmobileProcChainMask);
                c.Emit(OpCodes.Brfalse, label);
            } else {
                Log.Error("shockonhit IL Hook failed!");
            }

            //stunbullet
            c.Index = 0;
            if (c.TryGotoNext(MoveType.Before,
                    x => x.MatchBrfalse(out label),
                    x => x.MatchLdarg(0),
                    x => x.MatchLdcR4(1),
                    x => x.MatchCallOrCallvirt(typeof(SetStateOnHurt), nameof(SetStateOnHurt.SetStun))
                ))
            {
                c.Index++;
                c.Emit(OpCodes.Ldarg_1);
                c.EmitDelegate<Func<DamageReport, bool>>(checkImmobileProcChainMask);
                c.Emit(OpCodes.Brfalse, label);
            } else {
                Log.Error("stunbullet IL Hook failed!");
            }

            //immobilizestate
            c.Index = 0;
            if (c.TryGotoNext(MoveType.Before,
                    x => x.MatchBrfalse(out label),
                    x => x.MatchLdarg(0),
                    x => x.MatchLdloc(out _),
                    x => x.MatchLdfld(typeof(DamageInfo), nameof(DamageInfo.attacker)),
                    x => x.MatchLdloc(out _),
                    x => x.MatchLdfld(typeof(DamageInfo), nameof(DamageInfo.inflictor)),
                    x => x.MatchCallOrCallvirt(typeof(SetStateOnHurt), nameof(SetStateOnHurt.SetImmobilize))
                ))
            {
                c.Index++;
                c.Emit(OpCodes.Ldarg_1);
                c.EmitDelegate<Func<DamageReport, bool>>(checkImmobileProcChainMask);
                c.Emit(OpCodes.Brfalse, label);
            } else {
                Log.Error("immobilizestate IL Hook failed!");
            }
            
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
            CharacterBody _body;
            bool wasInFrozenState;

            private void Awake()
            {
                _entityStateMachine = GetComponent<EntityStateMachine>();
                _body = GetComponent<CharacterBody>();
                if(!_entityStateMachine || !_body)
                {
                    Destroy(this);
                }
            }

            private void FixedUpdate()
            {
                if(damage == 0 || !_body.healthComponent)
                {
                    Destroy(this);
                    return;
                }

                if (!IsImmobile(_entityStateMachine))
                {
                    dealDelayedDamage();
                    Destroy(this);
                }
                wasInFrozenState = _body.healthComponent.isInFrozenState;
            }

            void dealDelayedDamage() {
                ProcChainMask procChainMask = default(ProcChainMask);
                procChainMask.AddModdedProc(ProcTypes.Immobilize);

                if (wasInFrozenState)
                {
                    _body.healthComponent.isInFrozenState = true;
                }

                DamageInfo damageInfo = new DamageInfo
                {
                    damage = damage,
                    inflictor = attacker,
                    attacker = attacker,
                    procChainMask = procChainMask,
                    procCoefficient = 1,
                    damageColorIndex = DamageColorIndex.DelayedDamage,
                    damageType = DamageTypeExtended.BypassDamageCalculations,
                    position = _body.corePosition,
                };
                _body.healthComponent.TakeDamage(damageInfo);
                GlobalEventManager.instance.OnHitEnemy(damageInfo, _body.healthComponent.gameObject);
                GlobalEventManager.instance.OnHitAll(damageInfo, _body.healthComponent.gameObject);
                _body.healthComponent.isInFrozenState = false;
            }
        }
    }
}
