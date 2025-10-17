using ItemQualities.Utilities.Extensions;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using System;

namespace ItemQualities.Items
{
    static class SpeedBoostPickup
    {
        [SystemInitializer]
        static void Init()
        {
            IL.RoR2.CharacterBody.GetElusiveAntlersCurrentMaxStack += ItemHooks.CombineGroupedItemCountsPatch;
            IL.RoR2.ElusiveAntlersBehavior.FixedUpdate += ItemHooks.CombineGroupedItemCountsPatch;
            IL.RoR2.ElusiveAntlersPickup.OnTriggerStay += ItemHooks.CombineGroupedItemCountsPatch;

            IL.RoR2.CharacterBody.UpdateBuffs += CharacterBody_UpdateBuffs;
        }

        static void CharacterBody_UpdateBuffs(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (!c.TryFindNext(out ILCursor[] foundCursors,
                               x => x.MatchCall<CharacterBody>(nameof(CharacterBody.RemoveBuff)),
                               x => x.MatchRet()))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            VariableDefinition numRemovedAntlerBuffsVar = il.AddVariable<int>();

            c.Goto(foundCursors[0].Next, MoveType.Before); // call CharacterBody.RemoveBuff

            c.Emit(OpCodes.Dup);
            c.Emit(OpCodes.Ldloca, numRemovedAntlerBuffsVar);
            c.EmitDelegate<OnRemoveBuffDelegate>(onRemoveBuff);

            static void onRemoveBuff(BuffIndex buffIndex, ref int numRemovedAntlerBuffs)
            {
                if (buffIndex == DLC2Content.Buffs.ElusiveAntlersBuff.buffIndex)
                {
                    numRemovedAntlerBuffs++;
                }
            }

            c.Goto(foundCursors[1].Next, MoveType.AfterLabel); // ret

            c.Emit(OpCodes.Ldarg_0);
            c.Emit(OpCodes.Ldloc, numRemovedAntlerBuffsVar);
            c.EmitDelegate<Action<CharacterBody, int>>(tryRefundAntlerBuffs);

            static void tryRefundAntlerBuffs(CharacterBody body, int numRemovedAntlerBuffs)
            {
                if (numRemovedAntlerBuffs > 1)
                {
                    Inventory inventory = body ? body.inventory : null;

                    ItemQualityCounts speedBoostPickup = ItemQualitiesContent.ItemQualityGroups.SpeedBoostPickup.GetItemCounts(inventory);
                    if (speedBoostPickup.TotalQualityCount > 0)
                    {
                        float decayStepDuration = (1f * speedBoostPickup.UncommonCount) +
                                                  (3f * speedBoostPickup.RareCount) +
                                                  (5f * speedBoostPickup.EpicCount) +
                                                  (8f * speedBoostPickup.LegendaryCount);

                        for (int i = numRemovedAntlerBuffs - 1; i > 0; i--)
                        {
                            body.AddTimedBuff(DLC2Content.Buffs.ElusiveAntlersBuff, i * decayStepDuration);
                        }
                    }
                }
            }
        }

        delegate void OnRemoveBuffDelegate(BuffIndex buffIndex, ref int numRemovedAntlerBuffs);
    }
}
