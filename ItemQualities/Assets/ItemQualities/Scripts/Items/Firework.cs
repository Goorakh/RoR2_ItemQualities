using ItemQualities.Utilities.Extensions;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using System;
using UnityEngine;

namespace ItemQualities.Items
{
    static class Firework
    {
        [SystemInitializer]
        static void Init()
        {
            IL.RoR2.FireworkLauncher.FireMissile += FireworkLauncher_FireMissile;
        }

        static void FireworkLauncher_FireMissile(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            VariableDefinition shouldFireLargeFireworkVar = il.AddVariable<bool>();

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<FireworkLauncher, bool>>(getShouldFireLargeFirework);
            c.Emit(OpCodes.Stloc, shouldFireLargeFireworkVar);

            static bool getShouldFireLargeFirework(FireworkLauncher fireworkLauncher)
            {
                GameObject owner = fireworkLauncher ? fireworkLauncher.owner : null;
                CharacterBody ownerBody = owner ? owner.GetComponent<CharacterBody>() : null;
                Inventory ownerInventory = ownerBody ? ownerBody.inventory : null;

                float largeFireworkChance = 0f;
                if (ownerInventory)
                {
                    ItemQualityCounts firework = ItemQualitiesContent.ItemQualityGroups.Firework.GetItemCounts(ownerInventory);

                    largeFireworkChance = (10f * firework.UncommonCount) +
                                          (20f * firework.RareCount) +
                                          (40f * firework.EpicCount) +
                                          (60f * firework.LegendaryCount);
                }

                return Util.CheckRoll(largeFireworkChance, ownerBody ? ownerBody.master : null);
            }

            int patchCount = 0;

            while (c.TryGotoNext(MoveType.After,
                                 x => x.MatchLdfld<FireworkLauncher>(nameof(FireworkLauncher.projectilePrefab))))
            {
                c.Emit(OpCodes.Ldloc, shouldFireLargeFireworkVar);
                c.EmitDelegate<Func<GameObject, bool, GameObject>>(getProjectilePrefab);

                static GameObject getProjectilePrefab(GameObject projectilePrefab, bool shouldFireLargeFirework)
                {
                    if (shouldFireLargeFirework && ItemQualitiesContent.ProjectilePrefabs.FireworkProjectileBig)
                    {
                        projectilePrefab = ItemQualitiesContent.ProjectilePrefabs.FireworkProjectileBig;
                    }

                    return projectilePrefab;
                }

                patchCount++;
            }

            if (patchCount == 0)
            {
                Log.Error("Failed to find projectile prefab patch location");
            }
            else
            {
                Log.Debug($"Found {patchCount} projectile prefab patch location(s)");
            }

            patchCount = 0;

            c.Index = 0;

            while (c.TryGotoNext(MoveType.After,
                                 x => x.MatchLdfld<FireworkLauncher>(nameof(FireworkLauncher.damageCoefficient))))
            {
                c.Emit(OpCodes.Ldloc, shouldFireLargeFireworkVar);
                c.EmitDelegate<Func<float, bool, float>>(getDamageCoefficient);

                static float getDamageCoefficient(float damageCoefficient, bool shouldFireLargeFirework)
                {
                    if (shouldFireLargeFirework)
                    {
                        damageCoefficient = 5f;
                    }

                    return damageCoefficient;
                }

                patchCount++;
            }

            if (patchCount == 0)
            {
                Log.Error("Failed to find damage coefficient patch location");
            }
            else
            {
                Log.Debug($"Found {patchCount} damage coefficient patch location(s)");
            }
        }
    }
}
