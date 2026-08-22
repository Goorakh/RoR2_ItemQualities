using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using System;

namespace ItemQualities.Items
{
    public static class FireballsOnHit
    {
        [SystemInitializer]
        static void Init()
        {
            IL.RoR2.GlobalEventManager.ProcessHitEnemy += ProcessHitEnemy;
        }

        public static float GetFireballScaleIncrease(CharacterBody ownerBody)
        {
            Inventory inventory = ownerBody ? ownerBody.inventory : null;
            if (!inventory)
                return 1f;

            ItemQualityCounts fireballsOnHit = inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.FireballsOnHit);
            if (fireballsOnHit.TotalQualityCount <= 0)
                return 0f;

            float scaleIncrease = fireballsOnHit.HighestQuality switch
            {
                QualityTier.Uncommon => 3,
                QualityTier.Rare => 6,
                QualityTier.Epic => 9,
                QualityTier.Legendary => 13,
                _ => 0
            };

            return scaleIncrease;
        }

        private static void ProcessHitEnemy(ILContext il)
        {
            if (!il.Method.TryFindParameter<DamageInfo>(out ParameterDefinition damageInfoParameter))
            {
                Log.Error("Failed to find damageInfo parameter");
                return;
            }

            ILCursor c = new ILCursor(il);

            if (c.TryGotoNext(
                x => x.MatchLdsfld(typeof(RoR2Content.Items), nameof(RoR2Content.Items.FireballsOnHit)))
                && c.TryGotoNext(MoveType.After,
                x => x.MatchLdcI4(out _),
                x => x.MatchStloc(out _)
                ))
            {
                c.Index--;
                c.Emit(OpCodes.Ldarg, damageInfoParameter);
                c.EmitDelegate<Func<int, DamageInfo, int>>(increaseFireballCount);
            } else {
                Log.Error("Failed to find patch location");
            }

            int increaseFireballCount(int fireballs, DamageInfo damageInfo)
            {
                if (!damageInfo.attacker)
                    return fireballs;
                if (!damageInfo.attacker.TryGetComponent(out CharacterBody body) || !body.inventory)
                    return fireballs;

                ItemQualityCounts fireballsOnHit = body.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.FireballsOnHit);
                fireballs +=    fireballsOnHit.UncommonCount * 2 +
                                fireballsOnHit.RareCount * 4 +
                                fireballsOnHit.EpicCount * 6 +
                                fireballsOnHit.LegendaryCount * 8;
                return fireballs;
            }
        }
    }
}
