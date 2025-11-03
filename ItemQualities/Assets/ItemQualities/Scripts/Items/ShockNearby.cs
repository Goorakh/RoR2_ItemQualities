using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using RoR2.Items;
using System;

namespace ItemQualities.Items
{
    static class ShockNearby
    {
        [SystemInitializer]
        static void Init()
        {
            IL.RoR2.Items.ShockNearbyBodyBehavior.FixedUpdate += Il_ShockNearbyBodyBehavior_FixedUpdate;
        }

        static float ModifyInterval(float duration, ShockNearbyBodyBehavior behavior)
        {
            return ModifyInterval(duration, behavior.body);
        }

        static float ModifyInterval(float interval, CharacterBody body)
        {
            const float MinimumMultiplier = 0.1f;

            Inventory inventory = body ? body.inventory : null;
            ItemQualityCounts shockNearby = default;
            if (inventory)
                shockNearby = ItemQualitiesContent.ItemQualityGroups.ShockNearby.GetItemCounts(inventory);

            float multiplier = 1f;
            multiplier -= 0.1f * shockNearby.UncommonCount;
            multiplier -= 0.2f * shockNearby.RareCount;
            multiplier -= 0.3f * shockNearby.EpicCount;
            multiplier -= 0.5f * shockNearby.LegendaryCount;

            return interval * Math.Max(multiplier, MinimumMultiplier);
        }

        static void Il_ShockNearbyBodyBehavior_FixedUpdate(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            string[] fields = new string[]
            {
                nameof(ShockNearbyBodyBehavior.teslaBuffRollTimer),
                nameof(ShockNearbyBodyBehavior.teslaFireTimer),
                nameof(ShockNearbyBodyBehavior.teslaResetListTimer), // initial check
                nameof(ShockNearbyBodyBehavior.teslaResetListTimer), // actual reset
            };

            foreach (string field in fields)
            {
                if (!c.TryFindNext(out ILCursor[] foundCursors,
                                    x => x.MatchLdfld<ShockNearbyBodyBehavior>(field),
                                    x => x.MatchBltUn(out _) || x.MatchSub()))
                {
                    Log.Error($"Failed to find ShockNearbyBodyBehavior.{field} patch location");
                    return;
                }

                c.Goto(foundCursors[1].Next, MoveType.Before);
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate<Func<float, ShockNearbyBodyBehavior, float>>(ModifyInterval);
            }
        }
    }
}