using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using RoR2.Items;
using System;

namespace ItemQualities.Items
{
    static class Mushroom
    {
        [SystemInitializer]
        static void Init()
        {
            IL.RoR2.Items.MushroomBodyBehavior.FixedUpdate += IL_MushroomBodyBehavior_FixedUpdate;
            On.RoR2.Items.MushroomBodyBehavior.FixedUpdate += On_MushroomBodyBehavior_FixedUpdate;
        }

        static void IL_MushroomBodyBehavior_FixedUpdate(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (c.TryGotoNext(MoveType.Before,
                              x => x.MatchCallOrCallvirt<CharacterBody>(nameof(CharacterBody.GetNotMoving))))
            {
                c.Emit(OpCodes.Dup);
                c.Index++;
                c.EmitDelegate<Func<CharacterBody, bool, bool>>(getNotMoving);

                static bool getNotMoving(CharacterBody body, bool notMoving)
                {
                    if (body && body.TryGetComponent(out CharacterBodyExtraStatsTracker extraStatsTracker))
                        return extraStatsTracker.MushroomActiveServer;

                    return notMoving;
                }
            }
            else
            {
                Log.Error("Failed to find NotMoving patch location");
            }
        }

        static void On_MushroomBodyBehavior_FixedUpdate(On.RoR2.Items.MushroomBodyBehavior.orig_FixedUpdate orig, MushroomBodyBehavior self)
        {
            orig(self);

            CharacterBody body = self ? self.body : null;
            Inventory inventory = body ? body.inventory : null;

            if (!self || !self.mushroomHealingWard)
                return;
            
            ItemQualityCounts mushroom = default;
            if (inventory)
            {
                mushroom = ItemQualitiesContent.ItemQualityGroups.Mushroom.GetItemCounts(inventory);
            }

            float healIntervalRateMultiplier = 1f;
            healIntervalRateMultiplier += 0.20f * mushroom.UncommonCount;
            healIntervalRateMultiplier += 0.40f * mushroom.RareCount;
            healIntervalRateMultiplier += 0.75f * mushroom.EpicCount;
            healIntervalRateMultiplier += 1.00f * mushroom.LegendaryCount;

            self.mushroomHealingWard.interval /= healIntervalRateMultiplier;
        }
    }
}
