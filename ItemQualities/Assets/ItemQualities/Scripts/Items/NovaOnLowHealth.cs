using ItemQualities.Networking;
using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using R2API;
using R2API.Networking;
using R2API.Networking.Interfaces;
using RoR2;
using System;
using UnityEngine;
using UnityEngine.Networking;

namespace ItemQualities.Items
{
    internal static class NovaOnLowHealth
    {
        public static float GetMiniNovaDamageCoefficient(in ItemQualityCounts itemCounts)
        {
            return (itemCounts.UncommonCount * 7f) +
                   (itemCounts.RareCount * 15f) +
                   (itemCounts.EpicCount * 30f) +
                   (itemCounts.LegendaryCount * 60f);
        }

        [SystemInitializer]
        private static void Init()
        {
            On.RoR2.BlastAttack.Fire += BlastAttack_Fire;

            IL.EntityStates.VagrantNovaItem.ReadyState.FixedUpdate += ReadyState_FixedUpdate;
            IL.EntityStates.VagrantNovaItem.ReadyState.OnDamaged += ReadyState_OnDamaged;
        }

        private static BlastAttack.Result BlastAttack_Fire(On.RoR2.BlastAttack.orig_Fire orig, BlastAttack self)
        {
            BlastAttack.Result result = orig(self);

            BlastAttackInfo blastAttackInfo = BlastAttackInfo.FromBlastAttack(self);
            if (NetworkServer.active)
            {
                OnBlastAttackFireServer(blastAttackInfo);
            }
            else
            {
                new NovaOnLowHealthClientBlastAttackMessage(blastAttackInfo).Send(NetworkDestination.Server);
            }

            return result;
        }

        public static void OnBlastAttackFireServer(in BlastAttackInfo blastAttackInfo)
        {
            if (!blastAttackInfo.procChainMask.HasModdedProc(ProcTypes.NovaOnLowHealthBlast) &&
                blastAttackInfo.attacker &&
                blastAttackInfo.attacker.TryGetComponent(out CharacterBody attackerBody) &&
                attackerBody.inventory)
            {
                ItemQualityCounts novaOnLowHealth = attackerBody.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.NovaOnLowHealth);
                QualityTier qualityTier = novaOnLowHealth.HighestQuality;

                float extraBlastChance = qualityTier switch
                {
                    QualityTier.Uncommon => 2f,
                    QualityTier.Rare => 4f,
                    QualityTier.Epic => 6f,
                    QualityTier.Legendary => 8f,
                    _ => 0f,
                };

                if (RollUtil.CheckRoll(extraBlastChance * blastAttackInfo.procCoefficient, attackerBody.master, blastAttackInfo.procChainMask.HasProc(ProcType.SureProc)))
                {
                    ProcChainMask procChainMask = blastAttackInfo.procChainMask;
                    procChainMask.AddModdedProc(ProcTypes.NovaOnLowHealthBlast);

                    GameObject miniVagrantNovaBlastObj = GameObject.Instantiate(ItemQualitiesContent.NetworkedPrefabs.MiniVagrantNovaBlast, blastAttackInfo.position, Quaternion.identity);

                    GenericOwnership genericOwnership = miniVagrantNovaBlastObj.GetComponent<GenericOwnership>();
                    genericOwnership.ownerObject = attackerBody.gameObject;

                    NovaOnLowHealthDelayBlast novaOnLowHealthDelayBlast = miniVagrantNovaBlastObj.GetComponent<NovaOnLowHealthDelayBlast>();
                    novaOnLowHealthDelayBlast.procChainMask = procChainMask;
                    novaOnLowHealthDelayBlast.procCoefficient = blastAttackInfo.procCoefficient;

                    NetworkServer.Spawn(miniVagrantNovaBlastObj);
                }
            }
        }

        private static void ReadyState_FixedUpdate(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            int patchCount = 0;

            while (c.TryGotoNext(MoveType.Before,
                                 x => x.MatchCallOrCallvirt(typeof(HealthComponent).GetProperty(nameof(HealthComponent.isHealthLow)).GetMethod)))
            {
                c.Emit(OpCodes.Dup);
                c.Index++;

                c.EmitDelegate<Func<HealthComponent, bool, bool>>(isUnderStealthKitThreshold);

                static bool isUnderStealthKitThreshold(HealthComponent healthComponent, bool isHealthLow)
                {
                    if (healthComponent && healthComponent.TryGetComponentCached(out CharacterBodyExtraStatsTracker extraStatsTracker))
                    {
                        isHealthLow = healthComponent.IsHealthBelowThreshold(extraStatsTracker.GenesisLoopActivationThreshold);
                    }

                    return isHealthLow;
                }

                patchCount++;
            }

            if (patchCount == 0)
            {
                Log.Error("Failed to find patch location");
            }
            else
            {
                Log.Debug($"Found {patchCount} patch location(s)");
            }
        }

        private static void ReadyState_OnDamaged(ILContext il)
        {
            if (!il.Method.TryFindParameter<DamageReport>(out ParameterDefinition damageReportParam))
            {
                Log.Error("Failed to find DamageReport parameter");
                return;
            }

            ILCursor c = new ILCursor(il);

            int patchCount = 0;

            while (c.TryGotoNext(MoveType.After,
                                 x => x.MatchLdarg(damageReportParam),
                                 x => x.MatchLdfld<DamageReport>(nameof(DamageReport.hitLowHealth))))
            {
                c.Emit(OpCodes.Ldarg, damageReportParam);
                c.EmitDelegate<Func<bool, DamageReport, bool>>(isUnderNovaThreshold);

                static bool isUnderNovaThreshold(bool hitLowHealth, DamageReport damageReport)
                {
                    if (damageReport?.victim && damageReport.victim.TryGetComponentCached(out CharacterBodyExtraStatsTracker extraStatsTracker))
                    {
                        hitLowHealth = damageReport.victim.IsHealthBelowThreshold(extraStatsTracker.GenesisLoopActivationThreshold);
                    }

                    return hitLowHealth;
                }

                patchCount++;
            }

            if (patchCount == 0)
            {
                Log.Error("Failed to find patch location");
            }
            else
            {
                Log.Debug($"Found {patchCount} patch location(s)");
            }
        }
    }
}
