using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using R2API;
using RoR2;
using RoR2.Artifacts;
using RoR2.Items;
using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace ItemQualities
{
    public static partial class DamageTypes
    {
        public static DamageAPI.ModdedDamageType Frost6s { get; private set; }

        public static DamageAPI.ModdedDamageType ForceAddToSharedSuffering { get; private set; }

        public static DamageAPI.ModdedDamageType BypassDrops { get; private set; }

        // TODO: Exclude Echo on-kills from achievement tracking
        public static DamageAPI.ModdedDamageType Echo { get; private set; }

        public static DamageAPI.ModdedDamageType Void { get; private set; }

        [SystemInitializer]
        private static void Init()
        {
            Frost6s = DamageAPI.ReserveDamageType();
            ForceAddToSharedSuffering = DamageAPI.ReserveDamageType();
            BypassDrops = DamageAPI.ReserveDamageType();
            Echo = DamageAPI.ReserveDamageType();
            Void = DamageAPI.ReserveDamageType();

            GlobalEventManager.onServerDamageDealt += onServerDamageDealt;

            IL.RoR2.GlobalEventManager.OnCharacterDeath += GlobalEventManager_OnCharacterDeath;

            On.RoR2.Artifacts.BombArtifactManager.OnServerCharacterDeath += BombArtifactManager_OnServerCharacterDeath;
            On.RoR2.Artifacts.DoppelgangerInvasionManager.OnCharacterDeathGlobal += DoppelgangerInvasionManager_OnCharacterDeathGlobal;
            On.RoR2.Artifacts.SacrificeArtifactManager.OnServerCharacterDeath += SacrificeArtifactManager_OnServerCharacterDeath;
            On.RoR2.Artifacts.TeamDeathArtifactManager.OnServerCharacterDeathGlobal += TeamDeathArtifactManager_OnServerCharacterDeathGlobal;

            On.RoR2.ArtifactTrialMissionController.CombatState.OnCharacterDeathGlobal += CombatState_OnCharacterDeathGlobal;
            On.PowerOrbKeySpawner.SpawnKey += PowerOrbKeySpawner_SpawnKey;

            On.RoR2.GlobalDeathRewards.OnCharacterDeathGlobal += GlobalDeathRewards_OnCharacterDeathGlobal;

            On.RoR2.Stats.StatManager.OnCharacterDeath += StatManager_OnCharacterDeath;
        }

        private static void onServerDamageDealt(DamageReport damageReport)
        {
            if (damageReport?.damageInfo == null)
                return;

            DamageInfo damageInfo = damageReport.damageInfo;

            GameObject attacker = damageReport.attacker;

            CharacterBody victimBody = damageReport.victimBody;
            HealthComponent victimHealthComponent = damageReport.victim;

            if (victimHealthComponent && victimBody)
            {
                if (damageInfo.damageType.HasModdedDamageType(Frost6s))
                {
                    if (!victimHealthComponent.isInFrozenState && !victimBody.HasBuff(DLC2Content.Buffs.FreezeImmune))
                    {
                        victimBody.AddTimedBuff(DLC2Content.Buffs.Frost, 6f, 6);
                    }
                }

                if (damageInfo.damageType.HasModdedDamageType(ForceAddToSharedSuffering))
                {
                    if (victimBody.teamComponent.teamIndex != TeamIndex.None && !victimBody.HasBuff(DLC3Content.Buffs.SharedSuffering))
                    {
                        if (attacker && attacker.TryGetComponent(out SharedSufferingItemBehaviour sharedSufferingItemBehaviour))
                        {
                            victimBody.AddBuff(DLC3Content.Buffs.SharedSuffering);
                            if (!sharedSufferingItemBehaviour.afflicted.Contains(victimBody))
                            {
                                sharedSufferingItemBehaviour.afflicted.Add(victimBody);
                                sharedSufferingItemBehaviour.afflictedDirty = true;
                            }
                        }
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool shouldBypassDrops(DamageReport damageReport)
        {
            return damageReport.damageInfo.damageType.HasModdedDamageType(BypassDrops) || isEchoed(damageReport);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool isEchoed(DamageReport damageReport)
        {
            return damageReport.damageInfo.damageType.HasModdedDamageType(Echo);
        }

        private static void GlobalEventManager_OnCharacterDeath(ILContext il)
        {
            if (!il.Method.TryFindParameter<DamageReport>(out ParameterDefinition damageReportParameter))
            {
                Log.Error("Failed to find DamageReport parameter");
                return;
            }

            ILCursor c = new ILCursor(il);

            void emitCheckShouldBypassDrops()
            {
                c.Emit(OpCodes.Ldarg, damageReportParameter);
                c.EmitDelegate<Func<DamageReport, bool>>(shouldBypassDrops);
            }

            void emitCheckIsEchoed()
            {
                c.Emit(OpCodes.Ldarg, damageReportParameter);
                c.EmitDelegate<Func<DamageReport, bool>>(isEchoed);
            }

            // Sonorous drop
            {
                c.Goto(0);

                /*
                 *  // if (attackerMaster.inventory.GetItemCountEffective(DLC2Content.Items.ItemDropChanceOnKill) > 0)
                 *  IL_12DB: ldloc.s   V_17
                 *  IL_12DD: callvirt  instance class RoR2.Inventory RoR2.CharacterMaster::get_inventory()
                 *  IL_12E2: ldsfld    class RoR2.ItemDef RoR2.DLC2Content/Items::ItemDropChanceOnKill
                 *  IL_12E7: callvirt  instance int32 RoR2.Inventory::GetItemCountEffective(class RoR2.ItemDef)
                 *  IL_12EC: ldc.i4.0
                 *  IL_12ED: ble       IL_13F9
                 */

                ILLabel afterSonorousLabel = null;
                if (c.TryGotoNext(MoveType.AfterLabel,
                                  x => x.MatchLdloc(out _), // attackerMaster
                                  x => x.MatchCallOrCallvirt<CharacterMaster>("get_" + nameof(CharacterMaster.inventory)),
                                  x => x.MatchLdsfld(typeof(DLC2Content.Items), nameof(DLC2Content.Items.ItemDropChanceOnKill)),
                                  x => x.MatchCallOrCallvirt<Inventory>(nameof(Inventory.GetItemCountEffective)),
                                  x => x.MatchLdcI4(0),
                                  x => x.MatchBle(out afterSonorousLabel)))
                {
                    emitCheckShouldBypassDrops();
                    c.Emit(OpCodes.Brtrue, afterSonorousLabel);
                }
                else
                {
                    Log.Error("Failed to find sonorous drop patch location");
                }
            }

            // Glacial death
            {
                c.Goto(0);

                /*
                 *  // if (victimBody.HasBuff(RoR2Content.Buffs.AffixWhite))
                 *  IL_0217: ldloc.3
                 *  IL_0218: ldsfld    class RoR2.BuffDef RoR2.RoR2Content/Buffs::AffixWhite
                 *  IL_021D: callvirt  instance bool RoR2.CharacterBody::HasBuff(class RoR2.BuffDef)
                 *  IL_0222: brfalse   IL_0345
                 */

                ILLabel afterGlacialDeathLabel = null;
                if (c.TryGotoNext(MoveType.AfterLabel,
                                  x => x.MatchLdloc(out _), // victimBody
                                  x => x.MatchLdsfld(typeof(RoR2Content.Buffs), nameof(RoR2Content.Buffs.AffixWhite)),
                                  x => x.MatchCallOrCallvirt<CharacterBody>(nameof(CharacterBody.HasBuff)),
                                  x => x.MatchBrfalse(out afterGlacialDeathLabel)))
                {
                    emitCheckIsEchoed();
                    c.Emit(OpCodes.Brtrue, afterGlacialDeathLabel);
                }
                else
                {
                    Log.Error_NoCallerPrefix("Failed to find glacial death patch location");
                }
            }

            // Malachite death
            {
                c.Goto(0);

                /*
                 *  // if (victimBody.HasBuff(RoR2Content.Buffs.AffixPoison))
                 *  IL_0345: ldloc.3
                 *  IL_0346: ldsfld    class RoR2.BuffDef RoR2.RoR2Content/Buffs::AffixPoison
                 *  IL_034B: callvirt  instance bool RoR2.CharacterBody::HasBuff(class RoR2.BuffDef)
                 *  IL_0350: brfalse.s IL_03A2
                 */

                ILLabel afterMalachiteDeathLabel = null;
                if (c.TryGotoNext(MoveType.AfterLabel,
                                  x => x.MatchLdloc(out _), // victimBody
                                  x => x.MatchLdsfld(typeof(RoR2Content.Buffs), nameof(RoR2Content.Buffs.AffixPoison)),
                                  x => x.MatchCallOrCallvirt<CharacterBody>(nameof(CharacterBody.HasBuff)),
                                  x => x.MatchBrfalse(out afterMalachiteDeathLabel)))
                {
                    emitCheckIsEchoed();
                    c.Emit(OpCodes.Brtrue, afterMalachiteDeathLabel);
                }
                else
                {
                    Log.Error_NoCallerPrefix("Failed to find malachite death patch location");
                }
            }

            // Soul wisp
            {
                c.Goto(0);

                /*
                 *  // if (RunArtifactManager.instance.IsArtifactEnabled(RoR2Content.Artifacts.wispOnDeath))
                 *  IL_03A2: call      class RoR2.RunArtifactManager RoR2.RunArtifactManager::get_instance()
                 *  IL_03A7: call      class RoR2.ArtifactDef RoR2.RoR2Content/Artifacts::get_wispOnDeath()
                 *  IL_03AC: callvirt  instance bool RoR2.RunArtifactManager::IsArtifactEnabled(class RoR2.ArtifactDef)
                 *  IL_03B1: brfalse   IL_0464
                 */

                ILLabel afterSoulWispSpawnLabel = null;
                if (c.TryGotoNext(MoveType.AfterLabel,
                                  x => x.MatchCallOrCallvirt<RunArtifactManager>("get_" + nameof(RunArtifactManager.instance)),
                                  x => x.MatchCallOrCallvirt(typeof(RoR2Content.Artifacts), "get_" + nameof(RoR2Content.Artifacts.wispOnDeath)),
                                  x => x.MatchCallOrCallvirt<RunArtifactManager>(nameof(RunArtifactManager.IsArtifactEnabled)),
                                  x => x.MatchBrfalse(out afterSoulWispSpawnLabel)))
                {
                    emitCheckIsEchoed();
                    c.Emit(OpCodes.Brtrue, afterSoulWispSpawnLabel);
                }
                else
                {
                    Log.Error_NoCallerPrefix("Failed to find soul wisp spawn patch location");
                }
            }

            // Mending death
            {
                c.Goto(0);

                /*
                 *  // if (victimBody.HasBuff(DLC1Content.Buffs.EliteEarth))
                 *  IL_0579: ldloc.3
                 *  IL_057A: ldsfld    class RoR2.BuffDef RoR2.DLC1Content/Buffs::EliteEarth
                 *  IL_057F: callvirt  instance bool RoR2.CharacterBody::HasBuff(class RoR2.BuffDef)
                 *  IL_0584: brfalse.s IL_05C4
                 */

                ILLabel afterMendingDeathLabel = null;
                if (c.TryGotoNext(MoveType.AfterLabel,
                                  x => x.MatchLdloc(out _), // victimBody
                                  x => x.MatchLdsfld(typeof(DLC1Content.Buffs), nameof(DLC1Content.Buffs.EliteEarth)),
                                  x => x.MatchCallOrCallvirt<CharacterBody>(nameof(CharacterBody.HasBuff)),
                                  x => x.MatchBrfalse(out afterMendingDeathLabel)))
                {
                    emitCheckIsEchoed();
                    c.Emit(OpCodes.Brtrue, afterMendingDeathLabel);
                }
                else
                {
                    Log.Error_NoCallerPrefix("Failed to find mending death patch location");
                }
            }

            // Voidtouched death
            {
                c.Goto(0);

                /*
                 *  // if (victimBody.HasBuff(DLC1Content.Buffs.EliteVoid))
                 *  IL_05C4: ldloc.3
                 *  IL_05C5: ldsfld    class RoR2.BuffDef RoR2.DLC1Content/Buffs::EliteVoid
                 *  IL_05CA: callvirt  instance bool RoR2.CharacterBody::HasBuff(class RoR2.BuffDef)
                 *  IL_05CF: brfalse.s IL_0630
                 */

                ILLabel afterVoidtouchedDeathLabel = null;
                if (c.TryGotoNext(MoveType.AfterLabel,
                                  x => x.MatchLdloc(out _), // victimBody
                                  x => x.MatchLdsfld(typeof(DLC1Content.Buffs), nameof(DLC1Content.Buffs.EliteVoid)),
                                  x => x.MatchCallOrCallvirt<CharacterBody>(nameof(CharacterBody.HasBuff)),
                                  x => x.MatchBrfalse(out afterVoidtouchedDeathLabel)))
                {
                    emitCheckIsEchoed();
                    c.Emit(OpCodes.Brtrue, afterVoidtouchedDeathLabel);
                }
                else
                {
                    Log.Error_NoCallerPrefix("Failed to find voidtouched death patch location");
                }
            }
        }

        private static void BombArtifactManager_OnServerCharacterDeath(On.RoR2.Artifacts.BombArtifactManager.orig_OnServerCharacterDeath orig, DamageReport damageReport)
        {
            if (!isEchoed(damageReport))
            {
                orig(damageReport);
            }
        }

        private static void DoppelgangerInvasionManager_OnCharacterDeathGlobal(On.RoR2.Artifacts.DoppelgangerInvasionManager.orig_OnCharacterDeathGlobal orig, DoppelgangerInvasionManager self, DamageReport damageReport)
        {
            if (!shouldBypassDrops(damageReport))
            {
                orig(self, damageReport);
            }
        }

        private static void SacrificeArtifactManager_OnServerCharacterDeath(On.RoR2.Artifacts.SacrificeArtifactManager.orig_OnServerCharacterDeath orig, DamageReport damageReport)
        {
            if (!shouldBypassDrops(damageReport))
            {
                orig(damageReport);
            }
        }

        private static void TeamDeathArtifactManager_OnServerCharacterDeathGlobal(On.RoR2.Artifacts.TeamDeathArtifactManager.orig_OnServerCharacterDeathGlobal orig, DamageReport damageReport)
        {
            if (!isEchoed(damageReport))
            {
                orig(damageReport);
            }
        }

        private static void CombatState_OnCharacterDeathGlobal(On.RoR2.ArtifactTrialMissionController.CombatState.orig_OnCharacterDeathGlobal orig, EntityStates.EntityState self, DamageReport damageReport)
        {
            if (!shouldBypassDrops(damageReport))
            {
                orig(self, damageReport);
            }
        }

        private static void PowerOrbKeySpawner_SpawnKey(On.PowerOrbKeySpawner.orig_SpawnKey orig, PowerOrbKeySpawner self, DamageReport damageReport)
        {
            if (!shouldBypassDrops(damageReport))
            {
                orig(self, damageReport);
            }
        }

        private static void GlobalDeathRewards_OnCharacterDeathGlobal(On.RoR2.GlobalDeathRewards.orig_OnCharacterDeathGlobal orig, GlobalDeathRewards self, DamageReport damageReport)
        {
            if (!shouldBypassDrops(damageReport))
            {
                orig(self, damageReport);
            }
        }

        private static void StatManager_OnCharacterDeath(On.RoR2.Stats.StatManager.orig_OnCharacterDeath orig, DamageReport damageReport)
        {
            if (!isEchoed(damageReport))
            {
                orig(damageReport);
            }
        }
    }
}
