using ItemQualities.Utilities.Extensions;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using RoR2.Orbs;
using System;
using UnityEngine;

namespace ItemQualities.Items
{
    static class NovaOnHeal
    {
        [SystemInitializer]
        static void Init()
        {
            IL.RoR2.HealthComponent.ServerFixedUpdate += HealthComponent_ServerFixedUpdate;
        }

        static void HealthComponent_ServerFixedUpdate(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            int devilOrbVariableIndex = -1;
            if (!c.TryFindNext(out ILCursor[] foundCursors,
                               x => x.MatchNewobj<DevilOrb>(),
                               x => x.MatchStloc(typeof(DevilOrb), il, out devilOrbVariableIndex),
                               x => x.MatchStfld<DevilOrb>(nameof(DevilOrb.isCrit))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Goto(foundCursors[2].Next, MoveType.After); // stfld DevilOrb.isCrit

            c.Emit(OpCodes.Ldloc, devilOrbVariableIndex);
            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Action<DevilOrb, HealthComponent>>(handleQualityItem);

            static void handleQualityItem(DevilOrb devilOrb, HealthComponent healthComponent)
            {
                if (devilOrb == null || !healthComponent || !healthComponent.body)
                    return;

                ItemQualityCounts novaOnHeal = ItemQualitiesContent.ItemQualityGroups.NovaOnHeal.GetItemCounts(healthComponent.body.inventory);
                if (novaOnHeal.TotalQualityCount > 0)
                {
                    float procCoefficient = novaOnHeal.HighestQuality switch
                    {
                        QualityTier.Uncommon => 0.4f,
                        QualityTier.Rare => 0.6f,
                        QualityTier.Epic => 0.8f,
                        QualityTier.Legendary => 1.0f,
                        _ => throw new NotImplementedException($"Quality tier {novaOnHeal.HighestQuality} is not implemented")
                    };

                    devilOrb.procCoefficient = Mathf.Max(devilOrb.procCoefficient, procCoefficient);

                    float damageIncrease = (0.2f * novaOnHeal.UncommonCount) +
                                           (0.4f * novaOnHeal.RareCount) +
                                           (0.8f * novaOnHeal.EpicCount) +
                                           (1.0f * novaOnHeal.LegendaryCount);

                    if (damageIncrease > 0f)
                    {
                        devilOrb.damageValue *= 1f + damageIncrease;
                    }
                }
            }
        }
    }
}
