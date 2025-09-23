using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using System;

namespace ItemQualities.Items
{
    static class FragileDamageBonus
    {
        [SystemInitializer]
        static void Init()
        {
            IL.RoR2.HealthComponent.UpdateLastHitTime += HealthComponent_UpdateLastHitTime;
        }

        static void HealthComponent_UpdateLastHitTime(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (!c.TryFindNext(out ILCursor[] foundCursors,
                               x => x.MatchLdfld<HealthComponent.ItemCounts>(nameof(HealthComponent.ItemCounts.fragileDamageBonus)),
                               x => x.MatchCallOrCallvirt(typeof(HealthComponent).GetProperty(nameof(HealthComponent.isHealthLow)).GetMethod),
                               x => x.MatchBrfalse(out _)))
            {
                Log.Error("Failed to find watch break location");
                return;
            }

            c.Goto(foundCursors[1].Next, MoveType.After);

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<bool, HealthComponent, bool>>(isUnderWatchThreshold);

            static bool isUnderWatchThreshold(bool isHealthLow, HealthComponent healthComponent)
            {
                if (healthComponent && healthComponent.TryGetComponent(out CharacterBodyExtraStatsTracker extraStatsTracker))
                {
                    isHealthLow = healthComponent.IsHealthBelowThreshold(extraStatsTracker.WatchBreakThreshold);
                }

                return isHealthLow;
            }

            c.Goto(foundCursors[2].Next, MoveType.After);

            Instruction watchBlockStartInstruction = c.Next;

            ILLabel afterBaseWatchLogicLabel = c.DefineLabel();

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<HealthComponent, bool>>(hasAnyBaseWatch);
            c.Emit(OpCodes.Brfalse, afterBaseWatchLogicLabel);

            static bool hasAnyBaseWatch(HealthComponent healthComponent)
            {
                return healthComponent &&
                       healthComponent.body &&
                       healthComponent.body.inventory &&
                       healthComponent.body.inventory.GetItemCount(DLC1Content.Items.FragileDamageBonus) > 0;
            }

            int patchCount = 0;

            while (c.TryGotoNext(MoveType.After,
                                 x => x.MatchLdfld<HealthComponent.ItemCounts>(nameof(HealthComponent.ItemCounts.fragileDamageBonus))))
            {
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate<Func<int, HealthComponent, int>>(getBaseWatchItemCount);

                static int getBaseWatchItemCount(int itemCount, HealthComponent healthComponent)
                {
                    if (healthComponent && healthComponent.body && healthComponent.body.inventory)
                    {
                        itemCount = healthComponent.body.inventory.GetItemCount(DLC1Content.Items.FragileDamageBonus);
                    }

                    return itemCount;
                }

                patchCount++;
            }

            if (patchCount == 0)
            {
                Log.Error("Failed to find base watch add/remove patch location");
            }
            else
            {
                Log.Debug($"Found {patchCount} base watch add/remove patch location(s)");
            }

            c.Goto(watchBlockStartInstruction.Next);
            if (c.TryGotoNext(MoveType.After,
                              x => x.MatchCallOrCallvirt<CharacterMasterNotificationQueue>(nameof(CharacterMasterNotificationQueue.SendTransformNotification))))
            {
                c.MarkLabel(afterBaseWatchLogicLabel);

                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate<Action<HealthComponent>>(handleQualityWatches);

                static void handleQualityWatches(HealthComponent healthComponent)
                {
                    CharacterBody body = healthComponent ? healthComponent.body : null;
                    Inventory inventory = body ? body.inventory : null;
                    if (!inventory)
                        return;

                    for (QualityTier qualityTier = 0; qualityTier < QualityTier.Count; qualityTier++)
                    {
                        ItemIndex watchItemIndex = ItemQualitiesContent.ItemQualityGroups.FragileDamageBonus.GetItemIndex(qualityTier);
                        ItemIndex watchBrokenItemIndex = ItemQualitiesContent.ItemQualityGroups.FragileDamageBonusConsumed.GetItemIndex(qualityTier);

                        int watchCount = inventory.GetItemCount(watchItemIndex);
                        if (watchCount > 0)
                        {
                            inventory.RemoveItem(watchItemIndex, watchCount);
                            inventory.GiveItem(watchBrokenItemIndex, watchCount);

                            CharacterMasterNotificationQueue.SendTransformNotification(body.master, watchItemIndex, watchBrokenItemIndex, CharacterMasterNotificationQueue.TransformationType.Default);
                        }
                    }
                }
            }
            else
            {
                Log.Error($"Failed to find quality watch break patch location");
            }
        }
    }
}
