using HG;
using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using R2API;
using RoR2;
using RoR2.Projectile;
using System;
using UnityEngine;
using UnityEngine.Networking;

namespace ItemQualities.Items
{
    static class IncreasePrimaryDamage
    {
        static readonly int _qualityBuffActivationThreshold = 5;

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
                if (!NetworkServer.active || damageInfo == null || !victim || !ItemQualitiesContent.ProjectilePrefabs.IncreasePrimaryDamageQualityDotZone)
                    return false;

                if (damageInfo.procChainMask.HasModdedProc(ProcTypes.IncreasePrimaryDamage))
                    return false;

                if (attackerLuminousBuffCount < _qualityBuffActivationThreshold)
                    return false;

                CharacterBody attackerBody = damageInfo.attacker ? damageInfo.attacker.GetComponent<CharacterBody>() : null;
                Inventory attackerInventory = attackerBody ? attackerBody.inventory : null;
                if (!attackerInventory)
                    return false;

                ItemQualityCounts increasePrimaryDamage = attackerInventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.IncreasePrimaryDamage);
                if (increasePrimaryDamage.TotalQualityCount <= 0)
                    return false;

                for (int i = 0; i < _qualityBuffActivationThreshold; i++)
                {
                    attackerBody.RemoveBuff(DLC2Content.Buffs.IncreasePrimaryDamageBuff);
                }

                attackerBody.TransmitItemBehavior(new CharacterBody.NetworkItemBehaviorData(DLC2Content.Items.IncreasePrimaryDamage.itemIndex, attackerBody.GetBuffCount(DLC2Content.Buffs.IncreasePrimaryDamageBuff)));

                Vector3 spawnPosition = damageInfo.position;
                if (Physics.SphereCast(spawnPosition, 1f, Vector3.down, out RaycastHit hit, 4.5f, LayerIndex.world.mask, QueryTriggerInteraction.Ignore))
                {
                    spawnPosition = hit.point;
                }

                float damageCoefficient = 4f + (8f * increasePrimaryDamage.UncommonCount) +
                                               (12f * increasePrimaryDamage.RareCount) +
                                               (14f * increasePrimaryDamage.EpicCount) +
                                               (16f * increasePrimaryDamage.LegendaryCount);

                ProcChainMask procChainMask = damageInfo.procChainMask;
                procChainMask.AddModdedProc(ProcTypes.IncreasePrimaryDamage);

                ProjectileManager.instance.FireProjectile(new FireProjectileInfo
                {
                    projectilePrefab = ItemQualitiesContent.ProjectilePrefabs.IncreasePrimaryDamageQualityDotZone,
                    owner = damageInfo.attacker,
                    position = spawnPosition,
                    rotation = Quaternion.identity,
                    damage = damageInfo.damage * damageCoefficient,
                    crit = damageInfo.crit,
                    damageColorIndex = DamageColorIndex.Luminous,
                    procChainMask = procChainMask
                });

                return true;
            }
        }

        static void IncreasePrimaryDamageEffectUpdater_LightUpRings(On.RoR2.IncreasePrimaryDamageEffectUpdater.orig_LightUpRings orig, IncreasePrimaryDamageEffectUpdater self, int ringsToLight)
        {
            // Default behavior assumes buffs will never go down to anything but 0, so decreasing the value doesn't properly disable lights

            if (self && self.itemDisplay)
            {
                CharacterModel.RendererInfo[] rendererInfos = self.itemDisplay.rendererInfos;
                if (rendererInfos != null)
                {
                    void resetMaterial(int rendererIndex)
                    {
                        if (ArrayUtils.IsInBounds(rendererInfos, rendererIndex))
                        {
                            rendererInfos[rendererIndex].defaultMaterial = self.unlitEnergyMaterial;
                        }
                    }

                    resetMaterial(self.itemDisplay_ShotRingBottomIndex);
                    resetMaterial(self.itemDisplay_ShotRingMiddleIndex);
                    resetMaterial(self.itemDisplay_ShotRingTopIndex);
                }
            }

            orig(self, ringsToLight);
        }
    }
}
