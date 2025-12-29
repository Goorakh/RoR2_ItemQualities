using EntityStates.Headstompers;
using ItemQualities.Utilities.Extensions;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using System;
using UnityEngine;

namespace ItemQualities.Items
{
    static class FallBoots
    {
        [SystemInitializer]
        static void Init()
        {      
            IL.EntityStates.Headstompers.HeadstompersFall.DoStompExplosionAuthority += HeadstompersFall_DoStompExplosionAuthority;
        }

        static void HeadstompersFall_DoStompExplosionAuthority(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            int maxDistancePatchCount = 0;

            c.Goto(0);
            while (c.TryGotoNext(MoveType.After,
                                 x => x.MatchLdsfld<HeadstompersFall>(nameof(HeadstompersFall.maxDistance))))
            {
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate<Func<float, HeadstompersFall, float>>(getMaxDistance);

                static float getMaxDistance(float maxDistance, HeadstompersFall self)
                {
                    Inventory inventory = self?.body ? self.body.inventory : null;
                    if (inventory)
                    {
                        ItemQualityCounts fallBoots = inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.FallBoots);
                        if (fallBoots.TotalQualityCount > 0)
                        {
                            float distanceMultiplier = Mathf.Pow(1f - 0.1f, fallBoots.UncommonCount) *
                                                       Mathf.Pow(1f - 0.2f, fallBoots.RareCount) *
                                                       Mathf.Pow(1f - 0.3f, fallBoots.EpicCount) *
                                                       Mathf.Pow(1f - 0.4f, fallBoots.LegendaryCount);

                            maxDistance *= distanceMultiplier;
                        }
                    }

                    return maxDistance;
                }

                maxDistancePatchCount++;
            }

            if (maxDistancePatchCount == 0)
            {
                Log.Error("Failed to find maxDistance patch location");
            }
            else
            {
                Log.Debug($"Found {maxDistancePatchCount} maxDistance patch location(s)");
            }

            int minimumDamageCoefficientPatchCount = 0;

            c.Goto(0);
            while (c.TryGotoNext(MoveType.After,
                                 x => x.MatchLdsfld<HeadstompersFall>(nameof(HeadstompersFall.minimumDamageCoefficient))))
            {
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate<Func<float, HeadstompersFall, float>>(getMinimumDamageCoefficient);

                static float getMinimumDamageCoefficient(float minimumDamageCoefficient, HeadstompersFall self)
                {
                    Inventory inventory = self?.body ? self.body.inventory : null;
                    if (inventory)
                    {
                        ItemQualityCounts fallBoots = inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.FallBoots);
                        if (fallBoots.TotalQualityCount > 0)
                        {
                            float damageCoefficientBonus = (1f * fallBoots.UncommonCount) +
                                                           (2f * fallBoots.RareCount) +
                                                           (3f * fallBoots.EpicCount) +
                                                           (5f * fallBoots.LegendaryCount);

                            minimumDamageCoefficient += damageCoefficientBonus;
                        }
                    }

                    return minimumDamageCoefficient;
                }

                minimumDamageCoefficientPatchCount++;
            }

            if (minimumDamageCoefficientPatchCount == 0)
            {
                Log.Error("Failed to find minimumDamageCoefficient patch location");
            }
            else
            {
                Log.Debug($"Found {minimumDamageCoefficientPatchCount} minimumDamageCoefficient patch location(s)");
            }

            int maximumDamageCoefficientPatchCount = 0;

            c.Goto(0);
            while (c.TryGotoNext(MoveType.After,
                                 x => x.MatchLdsfld<HeadstompersFall>(nameof(HeadstompersFall.maximumDamageCoefficient))))
            {
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate<Func<float, HeadstompersFall, float>>(getMaximumDamageCoefficient);

                static float getMaximumDamageCoefficient(float maximumDamageCoefficient, HeadstompersFall self)
                {
                    Inventory inventory = self?.body ? self.body.inventory : null;
                    if (inventory)
                    {
                        ItemQualityCounts fallBoots = inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.FallBoots);
                        if (fallBoots.TotalQualityCount > 0)
                        {
                            float damageCoefficientBonus = (10f * fallBoots.UncommonCount) +
                                                           (20f * fallBoots.RareCount) +
                                                           (30f * fallBoots.EpicCount) +
                                                           (50f * fallBoots.LegendaryCount);

                            maximumDamageCoefficient += damageCoefficientBonus;
                        }
                    }

                    return maximumDamageCoefficient;
                }

                maximumDamageCoefficientPatchCount++;
            }

            if (maximumDamageCoefficientPatchCount == 0)
            {
                Log.Error("Failed to find maximumDamageCoefficient patch location");
            }
            else
            {
                Log.Debug($"Found {maximumDamageCoefficientPatchCount} maximumDamageCoefficient patch location(s)");
            }
        }
    }
}
