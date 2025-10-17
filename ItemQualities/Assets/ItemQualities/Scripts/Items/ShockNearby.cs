using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using RoR2.Items;
using RoR2.Orbs;
using System;
using UnityEngine;

namespace ItemQualities.Items
{
    static class ShockNearby
    {
        [SystemInitializer]
        static void Init()
        {
            IL.RoR2.Items.ShockNearbyBodyBehavior.FixedUpdate += Il_ShockNearbyBodyBehavior_FixedUpdate;
            IL.RoR2.Orbs.LightningOrb.Begin += IL_LightningOrb_Begin;
        }

        static float ModifyInterval(float duration, ShockNearbyBodyBehavior behavior)
        {
            return ModifyInterval(duration, behavior.body);
        }

        static float ModifyInterval(float interval, CharacterBody body)
        {
            Inventory inventory = body ? body.inventory : null;
            QualityTier shockNearby = QualityTier.None;
            if (inventory)
                shockNearby = ItemQualitiesContent.ItemQualityGroups.ShockNearby.GetHighestQualityInInventory(inventory);

            float multiplier = shockNearby switch
            {
                QualityTier.Uncommon  => 1f - 0.1f,
                QualityTier.Rare      => 1f - 0.2f,
                QualityTier.Epic      => 1f - 0.4f,
                QualityTier.Legendary => 1f - 0.6f,
                _ => 1f,
            };

            return interval * multiplier;
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

        private static void IL_LightningOrb_Begin(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (!c.TryFindNext(out ILCursor[] foundCursors,
                                x => x.MatchLdstr("Prefabs/Effects/OrbEffects/TeslaOrbEffect"),
                                x => x.MatchStloc(0)))
            {
                Log.Error($"Failed to find LightningOrb.Begin patch location");
                return;
            }

            c.Goto(foundCursors[1].Next, MoveType.After);
            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Action<LightningOrb>>(ModifyDuration);

            static void ModifyDuration(LightningOrb orb)
            {
                if (!orb.attacker)
                    return;

                orb.duration = ModifyInterval(orb.duration, orb.attacker.GetComponent<CharacterBody>());
            }
        }
    }
}