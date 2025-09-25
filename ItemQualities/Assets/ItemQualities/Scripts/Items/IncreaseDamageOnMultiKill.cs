using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using System;
using UnityEngine;

namespace ItemQualities.Items
{
    static class IncreaseDamageOnMultiKill
    {
        [SystemInitializer]
        static void Init()
        {
            IL.RoR2.CharacterBody.AddIncreasedDamageMultiKillTime += CharacterBody_AddIncreasedDamageMultiKillTime;
            IL.RoR2.IncreaseDamageOnMultiKillItemDisplayUpdater.UpdateKillCounterText += IncreaseDamageOnMultiKillItemDisplayUpdater_UpdateKillCounterText;

            IL.RoR2.CharacterBody.UpdateMultiKill += CharacterBody_UpdateMultiKill;
        }

        static float getChronicExpansionBuffResetTime(float baseDuration, CharacterBody body)
        {
            Inventory inventory = body ? body.inventory : null;

            ItemQualityCounts increaseDamageOnMultiKill = default;
            if (inventory)
            {
                increaseDamageOnMultiKill = ItemQualitiesContent.ItemQualityGroups.IncreaseDamageOnMultiKill.GetItemCounts(inventory);
            }

            baseDuration += 1f * increaseDamageOnMultiKill.UncommonCount;
            baseDuration += 3f * increaseDamageOnMultiKill.RareCount;
            baseDuration += 5f * increaseDamageOnMultiKill.EpicCount;
            baseDuration += 7f * increaseDamageOnMultiKill.LegendaryCount;

            return baseDuration;
        }

        static void CharacterBody_AddIncreasedDamageMultiKillTime(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (!c.TryGotoNext(MoveType.Before,
                               x => x.MatchStfld<CharacterBody>(nameof(CharacterBody.increasedDamageKillTimer))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<float, CharacterBody, float>>(getChronicExpansionBuffResetTime);
        }

        static void IncreaseDamageOnMultiKillItemDisplayUpdater_UpdateKillCounterText(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (!c.TryGotoNext(MoveType.After,
                               x => x.MatchLdfld<IncreaseDamageOnMultiKillItemDisplayUpdater>(nameof(IncreaseDamageOnMultiKillItemDisplayUpdater.resetTime))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<float, IncreaseDamageOnMultiKillItemDisplayUpdater, float>>(getResetTime);

            static float getResetTime(float baseResetTime, IncreaseDamageOnMultiKillItemDisplayUpdater displayUpdater)
            {
                CharacterBody body = displayUpdater ? displayUpdater.body : null;
                return getChronicExpansionBuffResetTime(baseResetTime, body);
            }
        }

        static void CharacterBody_UpdateMultiKill(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (!c.TryFindNext(out ILCursor[] foundCursors,
                               x => x.MatchLdfld<CharacterBody>(nameof(CharacterBody.increasedDamageKillTimer)),
                               x => x.MatchBgtUn(out _),
                               x => x.MatchStfld<CharacterBody>(nameof(CharacterBody.increasedDamageKillTimer)),
                               x => x.MatchCallOrCallvirt<CharacterBody>(nameof(CharacterBody.SetBuffCount))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Goto(foundCursors[3].Next, MoveType.After);
            ILLabel afterVanillaBuffResetLabel = c.MarkLabel();

            c.Goto(foundCursors[2].Next, MoveType.After);

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<CharacterBody, bool>>(tryDoQualityBuffReset);

            c.Emit(OpCodes.Brtrue, afterVanillaBuffResetLabel);

            static bool tryDoQualityBuffReset(CharacterBody body)
            {
                Inventory inventory = body ? body.inventory : null;

                ItemQualityCounts increaseDamageOnMultiKill = default;
                if (inventory)
                {
                    increaseDamageOnMultiKill = ItemQualitiesContent.ItemQualityGroups.IncreaseDamageOnMultiKill.GetItemCounts(inventory);
                }

                int maxStacksToRemove;
                if (increaseDamageOnMultiKill.LegendaryCount > 0)
                {
                    maxStacksToRemove = 1;
                }
                else if (increaseDamageOnMultiKill.EpicCount > 0)
                {
                    maxStacksToRemove = 3;
                }
                else if (increaseDamageOnMultiKill.RareCount > 0)
                {
                    maxStacksToRemove = 5;
                }
                else if (increaseDamageOnMultiKill.UncommonCount > 0)
                {
                    maxStacksToRemove = 10;
                }
                else
                {
                    return false;
                }

                body.SetBuffCount(DLC2Content.Buffs.IncreaseDamageBuff.buffIndex, Mathf.Max(0, body.GetBuffCount(DLC2Content.Buffs.IncreaseDamageBuff) - maxStacksToRemove));

                int remainingBuffCount = body.GetBuffCount(DLC2Content.Buffs.IncreaseDamageBuff);

                body.increasedDamageKillCount = Mathf.Min(body.increasedDamageKillCount, remainingBuffCount);
                body.increasedDamageKillCountBuff = Mathf.Min(body.increasedDamageKillCountBuff, remainingBuffCount);

                if (remainingBuffCount > 0)
                {
                    body.AddIncreasedDamageMultiKillTime();
                }

                return true;
            }
        }
    }
}
