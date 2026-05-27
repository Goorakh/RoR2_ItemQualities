using ItemQualities.Orbs;
using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using RoR2.Orbs;
using System;
using UnityEngine;

namespace ItemQualities.Items
{
    static class LightningStrikeOnHit
    {
        [SystemInitializer]
        static void Init()
        {
            IL.RoR2.GlobalEventManager.ProcessHitEnemy += ProcessHitEnemy;
        }

        private static void ProcessHitEnemy(ILContext il)
        {
            ILCursor c = new ILCursor(il);
            int characterBodyLoc = 0;

            if (!il.Method.TryFindParameter<DamageInfo>(out ParameterDefinition damageInfoParameter))
            {
                Log.Error("Failed to find DamageInfo parameter");
                return;
            }

            if (!c.TryGotoNext(MoveType.After,
                x => x.MatchLdfld(typeof(DamageInfo), nameof(DamageInfo.attacker)),
                x => x.MatchCallOrCallvirt(typeof(GameObject), nameof(GameObject.GetComponent))
            ) || !c.TryGotoNext(MoveType.After,
                x => x.MatchStloc(out characterBodyLoc)
            ))
            {
                Log.Error("IL Hook failed!");
                return;
            }

            if (!c.TryGotoNext(MoveType.After,
                x => x.MatchNewobj(typeof(SimpleLightningStrikeOrb))
            ))
            {
                Log.Error("IL Hook failed!");
                return;
            }

            c.Emit(OpCodes.Ldloc, characterBodyLoc);
            c.Emit(OpCodes.Ldarg, damageInfoParameter);
            c.EmitDelegate<Func<GenericDamageOrb, CharacterBody, DamageInfo, GenericDamageOrb>>(upgradeLightning);
            GenericDamageOrb upgradeLightning(GenericDamageOrb genericDamageOrb, CharacterBody characterBody, DamageInfo damageInfo)
            {
                if (!characterBody)
                    return genericDamageOrb;
                
                ItemQualityCounts lightningStrikeOnHit = characterBody.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.LightningStrikeOnHit);
                float upgradeChance = lightningStrikeOnHit.HighestQuality switch
                {
                    QualityTier.Uncommon => 10,
                    QualityTier.Rare => 12,
                    QualityTier.Epic => 14,
                    QualityTier.Legendary => 16,
                    _ => 0
                };

                if (RollUtil.CheckRoll(upgradeChance, characterBody.master, damageInfo.procChainMask.HasProc(ProcType.SureProc)))
                {
                    return new LightningUpgradeOrb();
                }
                return genericDamageOrb;
            }

            if (!c.TryGotoNext(MoveType.Before,
                x => x.MatchCallOrCallvirt(typeof(OrbManager), nameof(OrbManager.AddOrb))
            ))
            {
                Log.Error("IL Hook failed!");
                return;
            }

            c.Emit(OpCodes.Dup);
            c.Emit(OpCodes.Ldloc, characterBodyLoc);
            c.Emit(OpCodes.Ldarg, damageInfoParameter);
            c.EmitDelegate<Action<GenericDamageOrb, CharacterBody, DamageInfo>>(alterLightning);
            void alterLightning(GenericDamageOrb genericDamageOrb, CharacterBody characterBody, DamageInfo damageInfo)
            {
                if (!characterBody || damageInfo == null)
                    return;
                if (genericDamageOrb is not LightningUpgradeOrb)
                    return;
                ItemQualityCounts lightningStrikeOnHit = characterBody.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.LightningStrikeOnHit);
                float damageCoeff = lightningStrikeOnHit.UncommonCount * 5 +
                                    lightningStrikeOnHit.RareCount * 10 +
                                    lightningStrikeOnHit.EpicCount * 15 +
                                    lightningStrikeOnHit.LegendaryCount * 20;

                genericDamageOrb.damageValue += Util.OnHitProcDamage(damageInfo.damage, characterBody.damage, damageCoeff);
            }
        }
    }
}

