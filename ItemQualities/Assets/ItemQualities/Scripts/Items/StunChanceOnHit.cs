using EntityStates;
using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using R2API;
using RoR2;
using System;
using UnityEngine;

namespace ItemQualities.Items
{
    internal static class StunChanceOnHit
    {
        [SystemInitializer]
        private static void Init()
        {
            IL.RoR2.SetStateOnHurt.OnTakeDamageServer += SetStateOnHurt_OnTakeDamageServer;
            IL.RoR2.GlobalEventManager.ProcessHitEnemy += GlobalEventManager_ProcessHitEnemy;
            GlobalEventManager.onServerDamageDealt += onServerDamageDealt;
        }

        private static DelayedHitHandler onImmobilizeInternal(CharacterBody attacker, CharacterBody victim)
        {
            DelayedHitHandler delayedHitHandler = victim.GetComponent<DelayedHitHandler>();
            if (!delayedHitHandler)
            {
                delayedHitHandler = victim.gameObject.AddComponent<DelayedHitHandler>();
                delayedHitHandler.attacker = attacker;
            }

            return delayedHitHandler;
        }

        public static void OnImmobilize(CharacterBody attacker, CharacterBody victim)
        {
            onImmobilizeInternal(attacker, victim);
        }

        public static bool IsImmobile(EntityStateMachine entityStateMachine)
        {
            CharacterBody body = entityStateMachine.commonComponents.characterBody;

            if (entityStateMachine.state is StunState ||
                entityStateMachine.state is FrozenState ||
                entityStateMachine.state is ShockState)
            {
                return true;
            }

            if (body)
            {
                if (body.HasBuff(RoR2Content.Buffs.Entangle) ||
                    body.HasBuff(RoR2Content.Buffs.Nullified) ||
                    body.HasBuff(RoR2Content.Buffs.LunarSecondaryRoot) ||
                    body.HasBuff(DLC3Content.Buffs.VultureRoot) ||
                    body.HasBuff(DLC3Content.Buffs.Jailed) ||
                    // Generic immobilize check, does not work for bodies without base move speed so the buff checks are still necessary
                    (body.moveSpeed < Mathf.Epsilon && body.baseMoveSpeed > 0) ||
                    // Something in a vehicle is generally going to be immobile
                    body.currentVehicle)
                {
                    return true;
                }
            }

            return false;
        }

        private static void onServerDamageDealt(DamageReport report)
        {
            CharacterBody attackerbody = report.attackerBody;
            if (!attackerbody || !attackerbody.inventory)
                return;

            if (!report.victimBody.TryGetComponent(out DelayedHitHandler delayedHitHandler))
            {
                //fallback if something immobilizes directly, set the proc owner to the first person attacking after that instead, this should handle immobilizing attack automatically
                //things that are procced or don't deal damage still need to be handled manually, like quality opal
                EntityStateMachine bodyStateMachine = EntityStateMachine.FindByCustomName(report.victimBody.gameObject, "Body");
                if (bodyStateMachine && IsImmobile(bodyStateMachine))
                {
                    delayedHitHandler = onImmobilizeInternal(report.attackerBody, report.victimBody);
                }
            }

            if (!delayedHitHandler)
                return;

            ItemQualityCounts stunChanceOnHit = attackerbody.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.StunChanceOnHit);
            if (stunChanceOnHit.TotalQualityCount == 0)
                return;

            float multiplier = (stunChanceOnHit.UncommonCount * 0.15f) +
                               (stunChanceOnHit.RareCount * 0.3f) +
                               (stunChanceOnHit.EpicCount * 0.45f) +
                               (stunChanceOnHit.LegendaryCount * 0.6f);

            delayedHitHandler.damage += report.damageDealt * multiplier;
        }

        private static void GlobalEventManager_ProcessHitEnemy(ILContext il)
        {
            if (!il.Method.TryFindParameter<DamageInfo>(out ParameterDefinition damageInfoParameter))
            {
                Log.Error("Failed to find DamageInfo parameter");
                return;
            }

            if (!il.Method.TryFindParameter<GameObject>("victim", out ParameterDefinition victimParameter))
            {
                Log.Error("Failed to find victim parameter");
                return;
            }

            ILCursor c = new ILCursor(il);

            ILLabel label = null;
            //tentabauble
            if (c.TryGotoNext(
                    x => x.MatchLdsfld(typeof(RoR2Content.Buffs), nameof(RoR2Content.Buffs.Nullified))
                ) &&
                c.TryGotoPrev(MoveType.After,
                    x => x.MatchBrfalse(out label)
                ))
            {
                c.Emit(OpCodes.Ldarg, damageInfoParameter);
                c.Emit(OpCodes.Ldarg, victimParameter);
                c.EmitDelegate<Func<DamageInfo, GameObject, bool>>(tryImmobilize);
                c.Emit(OpCodes.Brfalse, label);
            }
            else
            {
                Log.Error(il.Method.Name + " IL Hook failed!");
                return;
            }

            static bool tryImmobilize(DamageInfo damageInfo, GameObject victim)
            {
                if (!damageInfo.procChainMask.HasModdedProc(ProcTypes.Immobilize))
                {
                    CharacterBody attackerBody = damageInfo.attacker ? damageInfo.attacker.GetComponent<CharacterBody>() : null;
                    CharacterBody victimBody = victim ? victim.GetComponent<CharacterBody>() : null;

                    OnImmobilize(attackerBody, victimBody);
                    return true;
                }

                return false;
            }
        }

        private static void SetStateOnHurt_OnTakeDamageServer(ILContext il)
        {
            if (!il.Method.TryFindParameter<DamageReport>(out ParameterDefinition damageReportParameter))
            {
                Log.Error("Failed to find DamageReport parameter");
                return;
            }

            ILCursor c = new ILCursor(il);

            ILLabel label = null;

            // stungrenade
            {
                c.Goto(0);

                if (c.TryGotoNext(
                        x => x.MatchCallOrCallvirt(typeof(SetStateOnHurt), nameof(SetStateOnHurt.SetStun))
                    ) &&
                    c.TryGotoPrev(MoveType.After,
                        x => x.MatchBrfalse(out label)
                    ))
                {
                    c.Emit(OpCodes.Ldarg, damageReportParameter);
                    c.EmitDelegate<Func<DamageReport, bool>>(tryImmobilize);
                    c.Emit(OpCodes.Brfalse, label);
                }
                else
                {
                    Log.Error("stungrenade IL Hook failed!");
                }
            }

            //freezeonhit
            {
                c.Goto(0);

                if (c.TryGotoNext(
                        x => x.MatchCallOrCallvirt(typeof(SetStateOnHurt), nameof(SetStateOnHurt.SetFrozen))
                    ) &&
                    c.TryGotoPrev(MoveType.After,
                        x => x.MatchBrfalse(out label)
                    ))
                {
                    c.Emit(OpCodes.Ldarg, damageReportParameter);
                    c.EmitDelegate<Func<DamageReport, bool>>(tryImmobilize);
                    c.Emit(OpCodes.Brfalse, label);
                }
                else
                {
                    Log.Error("freezeonhit IL Hook failed!");
                }
            }

            //shockonhit
            {
                c.Goto(0);

                if (c.TryGotoNext(
                        x => x.MatchCallOrCallvirt(typeof(SetStateOnHurt), nameof(SetStateOnHurt.SetShock))
                    ) &&
                    c.TryGotoPrev(MoveType.After,
                        x => x.MatchBrfalse(out label)
                    ))
                {
                    c.Emit(OpCodes.Ldarg, damageReportParameter);
                    c.EmitDelegate<Func<DamageReport, bool>>(tryImmobilize);
                    c.Emit(OpCodes.Brfalse, label);
                }
                else
                {
                    Log.Error("shockonhit IL Hook failed!");
                }
            }

            //stunbullet
            {
                c.Goto(0);

                if (c.TryGotoNext(MoveType.Before,
                        x => x.MatchBrfalse(out label),
                        x => x.MatchLdarg(0),
                        x => x.MatchLdcR4(out _),
                        x => x.MatchCallOrCallvirt(typeof(SetStateOnHurt), nameof(SetStateOnHurt.SetStun))
                    ))
                {
                    c.Index++;
                    c.Emit(OpCodes.Ldarg, damageReportParameter);
                    c.EmitDelegate<Func<DamageReport, bool>>(tryImmobilize);
                    c.Emit(OpCodes.Brfalse, label);
                }
                else
                {
                    Log.Error("stunbullet IL Hook failed!");
                }
            }

            //immobilizestate
            {
                c.Goto(0);

                if (c.TryGotoNext(
                        x => x.MatchCallOrCallvirt(typeof(SetStateOnHurt), nameof(SetStateOnHurt.SetImmobilize))
                    ) &&
                    c.TryGotoPrev(MoveType.After,
                        x => x.MatchBrfalse(out label)
                    ))
                {
                    c.Emit(OpCodes.Ldarg, damageReportParameter);
                    c.EmitDelegate<Func<DamageReport, bool>>(tryImmobilize);
                    c.Emit(OpCodes.Brfalse, label);
                }
                else
                {
                    Log.Error("immobilizestate IL Hook failed!");
                }
            }

            static bool tryImmobilize(DamageReport damageReport)
            {
                if (!damageReport.damageInfo.procChainMask.HasModdedProc(ProcTypes.Immobilize))
                {
                    OnImmobilize(damageReport.attackerBody, damageReport.victimBody);
                    return true;
                }

                return false;
            }
        }

        private sealed class DelayedHitHandler : MonoBehaviour
        {
            public float damage = 0;
            public CharacterBody attacker;

            private EntityStateMachine _entityStateMachine;
            private CharacterBody _body;
            private bool _wasInFrozenState;

            private void Awake()
            {
                _entityStateMachine = EntityStateMachine.FindByCustomName(gameObject, "Body");
                _body = GetComponent<CharacterBody>();
                if (!_entityStateMachine || !_body)
                {
                    Destroy(this);
                }
            }

            private void FixedUpdate()
            {
                if (damage == 0 || !_body.healthComponent)
                {
                    Destroy(this);
                    return;
                }

                if (!IsImmobile(_entityStateMachine))
                {
                    dealDelayedDamage();
                    Destroy(this);
                }

                _wasInFrozenState = _body.healthComponent.isInFrozenState;
            }

            private void dealDelayedDamage()
            {
                ProcChainMask procChainMask = new ProcChainMask();
                procChainMask.AddModdedProc(ProcTypes.Immobilize);

                bool restorefrozen = _body.healthComponent.isInFrozenState;
                _body.healthComponent.isInFrozenState = _wasInFrozenState;

                DamageInfo damageInfo = new DamageInfo
                {
                    damage = damage,
                    inflictor = attacker ? attacker.gameObject : null,
                    attacker = attacker ? attacker.gameObject : null,
                    procChainMask = procChainMask,
                    procCoefficient = 1,
                    damageColorIndex = DamageColorIndex.DelayedDamage,
                    damageType = DamageTypeExtended.BypassDamageCalculations,
                    position = _body.corePosition,
                };

                _body.healthComponent.TakeDamage(damageInfo);
                GlobalEventManager.instance.OnHitEnemy(damageInfo, _body.healthComponent.gameObject);
                GlobalEventManager.instance.OnHitAll(damageInfo, _body.healthComponent.gameObject);

                _body.healthComponent.isInFrozenState = restorefrozen;
            }
        }
    }
}
