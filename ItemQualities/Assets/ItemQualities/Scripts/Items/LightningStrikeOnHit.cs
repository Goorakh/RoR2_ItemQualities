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
    internal static class LightningStrikeOnHit
    {
        [SystemInitializer]
        private static void Init()
        {
            IL.RoR2.GlobalEventManager.ProcessHitEnemy += ProcessHitEnemy;
        }

        private static void ProcessHitEnemy(ILContext il)
        {
            ILCursor c = new ILCursor(il);
            VariableDefinition characterBodyLoc = null;

            if (!il.Method.TryFindParameter<DamageInfo>(out ParameterDefinition damageInfoParameter))
            {
                Log.Error("Failed to find DamageInfo parameter");
                return;
            }

            if (!c.TryGotoNext(MoveType.After,
                x => x.MatchLdfld(typeof(DamageInfo), nameof(DamageInfo.attacker)),
                x => x.MatchCallOrCallvirt(typeof(GameObject), nameof(GameObject.GetComponent))
            ) || !c.TryGotoNext(MoveType.After,
                x => x.MatchStloc(il, out characterBodyLoc)
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
            static GenericDamageOrb upgradeLightning(GenericDamageOrb genericDamageOrb, CharacterBody characterBody, DamageInfo damageInfo)
            {
                if (!characterBody)
                    return genericDamageOrb;
                
                ItemQualityCounts lightningStrikeOnHit = characterBody.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.LightningStrikeOnHit);
                float upgradeChance = lightningStrikeOnHit.HighestQuality switch
                {
                    QualityTier.Uncommon => 25,
                    QualityTier.Rare => 50,
                    QualityTier.Epic => 75,
                    QualityTier.Legendary => 100,
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
            static void alterLightning(GenericDamageOrb genericDamageOrb, CharacterBody characterBody, DamageInfo damageInfo)
            {
                if (!characterBody || !characterBody.inventory || damageInfo == null)
                    return;

                if (genericDamageOrb is not LightningUpgradeOrb lightningUpgradeOrb)
                    return;

                ItemQualityCounts lightningStrikeOnHit = characterBody.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.LightningStrikeOnHit);

                float damageCoeff = (lightningStrikeOnHit.UncommonCount * 8f) +
                                    (lightningStrikeOnHit.RareCount * 12f) +
                                    (lightningStrikeOnHit.EpicCount * 16f) +
                                    (lightningStrikeOnHit.LegendaryCount * 25f);

                lightningUpgradeOrb.damageValue += Util.OnHitProcDamage(damageInfo.damage, characterBody.damage, damageCoeff);

                lightningUpgradeOrb.baseBlastRadius += lightningStrikeOnHit.HighestQuality switch
                {
                    QualityTier.Uncommon => 4f,
                    QualityTier.Rare => 7f,
                    QualityTier.Epic => 12f,
                    QualityTier.Legendary => 17f,
                    _ => 0f,
                };
            }
        }
    }
}

