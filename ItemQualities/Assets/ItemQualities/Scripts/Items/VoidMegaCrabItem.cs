using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using System;

namespace ItemQualities.Items
{
    static class VoidMegaCrabItem
    {
        [SystemInitializer]
        static void Init()
        {
            IL.RoR2.VoidMegaCrabItemBehavior.FixedUpdate += VoidMegaCrabItemBehavior_FixedUpdate;
            On.RoR2.VoidMegaCrabItemBehavior.OnMasterSpawned += VoidMegaCrabItemBehavior_OnMasterSpawned;
        }

        static void VoidMegaCrabItemBehavior_FixedUpdate(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (!c.TryFindNext(out ILCursor[] foundCursors,
                               x => x.MatchCallOrCallvirt<CharacterMaster>(nameof(CharacterMaster.IsDeployableLimited)),
                               x => x.MatchLdfld<VoidMegaCrabItemBehavior>(nameof(VoidMegaCrabItemBehavior.spawnTimer)),
                               x => x.MatchDiv()))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Goto(foundCursors[2].Next, MoveType.Before); // div

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<float, VoidMegaCrabItemBehavior, float>>(getSpawnRate);

            static float getSpawnRate(float spawnRate, VoidMegaCrabItemBehavior itemBehavior)
            {
                CharacterBody body = itemBehavior ? itemBehavior.body : null;
                Inventory inventory = body ? body.inventory : null;

                if (inventory)
                {
                    ItemQualityCounts voidMegaCrabItem = ItemQualitiesContent.ItemQualityGroups.VoidMegaCrabItem.GetItemCountsEffective(inventory);
                    if (voidMegaCrabItem.TotalQualityCount > 0)
                    {
                        spawnRate += (0.1f * voidMegaCrabItem.UncommonCount) +
                                     (0.3f * voidMegaCrabItem.RareCount) +
                                     (0.5f * voidMegaCrabItem.EpicCount) +
                                     (1.0f * voidMegaCrabItem.LegendaryCount);
                    }
                }

                return spawnRate;
            }
        }

        static void VoidMegaCrabItemBehavior_OnMasterSpawned(On.RoR2.VoidMegaCrabItemBehavior.orig_OnMasterSpawned orig, VoidMegaCrabItemBehavior self, SpawnCard.SpawnResult spawnResult)
        {
            orig(self, spawnResult);

            CharacterBody body = self ? self.body : null;
            Inventory inventory = body ? body.inventory : null;

            ItemQualityCounts voidMegaCrabItem = ItemQualitiesContent.ItemQualityGroups.VoidMegaCrabItem.GetItemCountsEffective(inventory);
            if (voidMegaCrabItem.TotalQualityCount > 0)
            {
                int damageBoostAmount = (3 * voidMegaCrabItem.UncommonCount) +
                                        (5 * voidMegaCrabItem.RareCount) +
                                        (7 * voidMegaCrabItem.EpicCount) +
                                        (10 * voidMegaCrabItem.LegendaryCount);

                if (spawnResult.spawnedInstance && spawnResult.spawnedInstance.TryGetComponent(out CharacterMaster spawnedMaster) && spawnedMaster.inventory)
                {
                    spawnedMaster.inventory.GiveItemPermanent(RoR2Content.Items.BoostDamage, damageBoostAmount);
                }
            }
        }
    }
}
