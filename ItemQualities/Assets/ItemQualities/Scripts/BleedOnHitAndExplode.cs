using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using System;

namespace ItemQualities.Items
{
    public static class BleedOnHitAndExplode
    {
        [SystemInitializer]
        static void Init() 
        {
            IL.RoR2.GlobalEventManager.ProcessHitEnemy += ProcerssHitEnemy;
        }

        private static void ProcerssHitEnemy(ILContext il)
        {
            if (!il.Method.TryFindParameter<DamageInfo>(out ParameterDefinition damageInfoParameter))
            {
                Log.Error("Failed to find damageInfo parameter");
                return;
            }

            ILCursor c = new ILCursor(il);

            if (!c.TryGotoNext(
            x => x.MatchLdsfld(typeof(RoR2Content.Items), nameof(RoR2Content.Items.BleedOnHitAndExplode)))
            || !c.TryGotoNext(
            x => x.MatchCallOrCallvirt(typeof(DotController), nameof(DotController.InflictDot)))
            || !c.TryGotoPrev(MoveType.After,
            x => x.MatchLdcI4((int)DotController.DotIndex.Bleed)))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Emit(OpCodes.Ldarg, damageInfoParameter);
            c.EmitDelegate<Func<DotController.DotIndex, DamageInfo, DotController.DotIndex>>(UpgradeBleed);

            DotController.DotIndex UpgradeBleed(DotController.DotIndex bleedIndex, DamageInfo damageInfo)
            {
                if (!damageInfo.attacker)
                    return bleedIndex;
                if (!damageInfo.attacker.TryGetComponent(out CharacterBody body) || !body.inventory || !body.master)
                    return bleedIndex;
                if (!damageInfo.crit)
                    return bleedIndex;

                ItemQualityCounts bleedOnHitAndExplode = body.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.BleedOnHitAndExplode);
                if (bleedOnHitAndExplode.TotalQualityCount == 0)
                    return bleedIndex;
                float upgradeChance =   bleedOnHitAndExplode.UncommonCount * 5 +
                                        bleedOnHitAndExplode.RareCount * 10 +
                                        bleedOnHitAndExplode.EpicCount * 15 +
                                        bleedOnHitAndExplode.LegendaryCount * 25;

                if (RollUtil.CheckRoll(upgradeChance, body.master, damageInfo.procChainMask.HasProc(ProcType.SureProc)))
                {
                    return DotController.DotIndex.SuperBleed;
                }
                else
                {
                    return bleedIndex;
                }
            }

            c.Emit(OpCodes.Dup);

            if (!c.TryGotoNext(MoveType.After,
            x => x.MatchLdfld(typeof(DamageInfo), nameof(DamageInfo.procCoefficient)),
            x => x.MatchMul()))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Emit(OpCodes.Ldarg, damageInfoParameter);
            c.EmitDelegate<Func<DotController.DotIndex, float, DamageInfo, float>>(IncreaseDuration);
            float IncreaseDuration(DotController.DotIndex dotIndex, float duration, DamageInfo damageInfo)
            {
                if (dotIndex == DotController.DotIndex.SuperBleed)
                {
                    duration += 13 * damageInfo.procCoefficient;
                }
                return duration;
            }
        }
    }
}
