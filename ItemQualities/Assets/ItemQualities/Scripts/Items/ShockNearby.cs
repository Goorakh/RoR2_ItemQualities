using ItemQualities.Utilities.Extensions;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using RoR2.Items;
using System;
using UnityEngine;

namespace ItemQualities.Items
{
    static class ShockNearby
    {
        [SystemInitializer]
        static void Init()
        {
            IL.RoR2.Items.ShockNearbyBodyBehavior.FixedUpdate += ShockNearbyBodyBehavior_FixedUpdate;
        }

        static void ShockNearbyBodyBehavior_FixedUpdate(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            VariableDefinition intervalMultiplierVar = il.AddVariable<float>();

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<ShockNearbyBodyBehavior, float>>(getIntervalMultiplier);
            c.Emit(OpCodes.Stloc, intervalMultiplierVar);

            static float getIntervalMultiplier(ShockNearbyBodyBehavior shockNearbyBodyBehavior)
            {
                CharacterBody body = shockNearbyBodyBehavior ? shockNearbyBodyBehavior.body : null;
                Inventory inventory = body ? body.inventory : null;

                ItemQualityCounts shockNearby = ItemQualitiesContent.ItemQualityGroups.ShockNearby.GetItemCountsEffective(inventory);
                if (shockNearby.TotalQualityCount > 0)
                {
                    return Mathf.Pow(1f - 0.1f, shockNearby.UncommonCount) *
                           Mathf.Pow(1f - 0.2f, shockNearby.RareCount) *
                           Mathf.Pow(1f - 0.3f, shockNearby.EpicCount) *
                           Mathf.Pow(1f - 0.5f, shockNearby.LegendaryCount);
                }

                return 1f;
            }

            if (c.TryGotoNext(MoveType.After,
                              x => x.MatchLdfld<ShockNearbyBodyBehavior>(nameof(ShockNearbyBodyBehavior.teslaBuffRollTimer)),
                              x => x.MatchLdcR4(10f)))
            {
                c.Emit(OpCodes.Ldloc, intervalMultiplierVar);
                c.Emit(OpCodes.Mul);

                if (c.TryGotoNext(MoveType.Before,
                                  x => x.MatchLdcR4(0f),
                                  x => x.MatchStfld<ShockNearbyBodyBehavior>(nameof(ShockNearbyBodyBehavior.teslaBuffRollTimer))))
                {
                    c.Index++;

                    c.Emit(OpCodes.Pop);

                    // this.teslaBuffRollTimer - (10f * intervalMultiplierVar)
                    c.Emit(OpCodes.Ldarg_0);
                    c.Emit<ShockNearbyBodyBehavior>(OpCodes.Ldfld, nameof(ShockNearbyBodyBehavior.teslaBuffRollTimer));
                    c.Emit(OpCodes.Ldc_R4, 10f);
                    c.Emit(OpCodes.Ldloc, intervalMultiplierVar);
                    c.Emit(OpCodes.Mul);
                    c.Emit(OpCodes.Sub);
                }
                else
                {
                    Log.Warning("Failed to find buff roll reset patch location");
                }
            }
            else
            {
                Log.Error("Failed to find buff roll interval patch location");
            }

            int patchCount = 0;

            c.Index = 0;
            while (c.TryGotoNext(MoveType.After,
                                 x => x.MatchLdfld<ShockNearbyBodyBehavior>(nameof(ShockNearbyBodyBehavior.teslaResetListInterval))))
            {
                c.Emit(OpCodes.Ldloc, intervalMultiplierVar);
                c.Emit(OpCodes.Mul);
            }

            if (patchCount == 0)
            {
                Log.Warning("Failed to find list reset interval patch location");
            }
            else
            {
                Log.Debug($"Found {patchCount} list reset interval patch location(s)");
            }
        }
    }
}
