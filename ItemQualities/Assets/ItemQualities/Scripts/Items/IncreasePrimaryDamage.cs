using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using System;
using UnityEngine;
using UnityEngine.Networking;

namespace ItemQualities.Items
{
    static class IncreasePrimaryDamage
    {
        const int QualityLuminousBuffActivationThreshold = 5;

        [SystemInitializer]
        static void Init()
        {
            IL.RoR2.GlobalEventManager.ProcessHitEnemy += GlobalEventManager_ProcessHitEnemy;

            On.RoR2.IncreasePrimaryDamageEffectUpdater.LightUpRings += IncreasePrimaryDamageEffectUpdater_LightUpRings;
        }

        static void GlobalEventManager_ProcessHitEnemy(ILContext il)
        {
            ILCursor c = new ILCursor(il);

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

            int attackerLuminousBuffCountLocalIndex = -1;
            ILLabel afterLuminousBlockLabel = default;
            if (!c.TryFindNext(out ILCursor[] foundCursors,
                               x => x.MatchLdsfld(typeof(DLC2Content.Buffs), nameof(DLC2Content.Buffs.IncreasePrimaryDamageBuff)),
                               x => x.MatchCallOrCallvirt<CharacterBody>(nameof(CharacterBody.GetBuffCount)),
                               x => x.MatchStloc(typeof(int), il, out attackerLuminousBuffCountLocalIndex),
                               x => x.MatchLdloc(attackerLuminousBuffCountLocalIndex),
                               x => x.MatchBlt(out afterLuminousBlockLabel)))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Goto(foundCursors[4].Next, MoveType.After); // blt afterLuminousBlockLabel

            c.Emit(OpCodes.Ldarg, damageInfoParameter);
            c.Emit(OpCodes.Ldarg, victimParameter);
            c.Emit(OpCodes.Ldloc, attackerLuminousBuffCountLocalIndex);
            c.EmitDelegate<Func<DamageInfo, GameObject, int, bool>>(tryQualityLuminousProc);
            c.Emit(OpCodes.Brtrue, afterLuminousBlockLabel);

            static bool tryQualityLuminousProc(DamageInfo damageInfo, GameObject victim, int attackerLuminousBuffCount)
            {
                if (!NetworkServer.active || damageInfo == null || !victim)
                    return false;

                if (attackerLuminousBuffCount < QualityLuminousBuffActivationThreshold)
                    return false;

                CharacterBody attackerBody = damageInfo.attacker ? damageInfo.attacker.GetComponent<CharacterBody>() : null;
                Inventory attackerInventory = attackerBody ? attackerBody.inventory : null;

                ItemQualityCounts increasePrimaryDamage = ItemQualitiesContent.ItemQualityGroups.IncreasePrimaryDamage.GetItemCountsEffective(attackerInventory);
                if (increasePrimaryDamage.TotalQualityCount <= 0)
                    return false;

                Vector3 victimPosition = victim.transform.position;
                if (victim.TryGetComponent(out CharacterBody victimBody))
                {
                    victimPosition = victimBody.corePosition;
                }

                for (int i = 0; i < QualityLuminousBuffActivationThreshold; i++)
                {
                    attackerBody.RemoveBuff(DLC2Content.Buffs.IncreasePrimaryDamageBuff);
                }

                attackerBody.TransmitItemBehavior(new CharacterBody.NetworkItemBehaviorData(DLC2Content.Items.IncreasePrimaryDamage.itemIndex, attackerBody.GetBuffCount(DLC2Content.Buffs.IncreasePrimaryDamageBuff)));

                float blastProcCoefficient = increasePrimaryDamage.HighestQuality switch
                {
                    QualityTier.Uncommon => 1.25f,
                    QualityTier.Rare => 1.5f,
                    QualityTier.Epic => 1.75f,
                    QualityTier.Legendary => 2f,
                    _ => throw new NotImplementedException($"Quality tier {increasePrimaryDamage.HighestQuality} is not implemented")
                };

                float blastDamageCoefficient = 2.5f + (0.75f * increasePrimaryDamage.UncommonCount) +
                                                      (1.00f * increasePrimaryDamage.RareCount) +
                                                      (1.50f * increasePrimaryDamage.EpicCount) +
                                                      (2.00f * increasePrimaryDamage.LegendaryCount);

                GameObject delayBlastObj = GameObject.Instantiate(GlobalEventManager.CommonAssets.increasePrimaryDamageDelayBlastPrefab, victimPosition, Quaternion.identity);

                DelayBlast delayBlast = delayBlastObj.GetComponent<DelayBlast>();
                delayBlast.position = victimPosition;
                delayBlast.attacker = damageInfo.attacker;
                delayBlast.baseDamage = damageInfo.damage * blastDamageCoefficient;
                delayBlast.crit = damageInfo.crit;
                delayBlast.procCoefficient = blastProcCoefficient;
                delayBlast.radius = 7f;
                delayBlast.maxTimer = 0.2f;
                delayBlast.falloffModel = BlastAttack.FalloffModel.None;
                delayBlast.explosionEffect = GlobalEventManager.CommonAssets.increasePrimaryDamageImpactPrefab;
                delayBlast.damageColorIndex = DamageColorIndex.Luminous;

                if (delayBlastObj.TryGetComponent(out TeamFilter delayBlastTeamFilter))
                {
                    delayBlastTeamFilter.teamIndex = TeamComponent.GetObjectTeam(damageInfo.attacker);
                }

                NetworkServer.Spawn(delayBlastObj);

                return true;
            }
        }

        static void IncreasePrimaryDamageEffectUpdater_LightUpRings(On.RoR2.IncreasePrimaryDamageEffectUpdater.orig_LightUpRings orig, IncreasePrimaryDamageEffectUpdater self, int ringsToLight)
        {
            // Default behavior assumes buffs will never to down to anything but 0, so decreasing the value doesn't properly disable lights

            if (self.itemDisplay)
            {
                CharacterModel.RendererInfo[] rendererInfos = self.itemDisplay.rendererInfos;
                if (rendererInfos != null)
                {
                    if (self.itemDisplay_ShotRingTopIndex < rendererInfos.Length)
                        rendererInfos[self.itemDisplay_ShotRingTopIndex].defaultMaterial = self.unlitEnergyMaterial;

                    if (self.itemDisplay_ShotRingMiddleIndex < rendererInfos.Length)
                        rendererInfos[self.itemDisplay_ShotRingMiddleIndex].defaultMaterial = self.unlitEnergyMaterial;

                    if (self.itemDisplay_ShotRingBottomIndex < rendererInfos.Length)
                        rendererInfos[self.itemDisplay_ShotRingBottomIndex].defaultMaterial = self.unlitEnergyMaterial;
                }
            }

            orig(self, ringsToLight);
        }
    }
}
