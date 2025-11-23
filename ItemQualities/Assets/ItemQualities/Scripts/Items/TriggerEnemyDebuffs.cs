using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using RoR2.Orbs;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ItemQualities.Items
{
    static class TriggerEnemyDebuffs
    {
        [SystemInitializer]
        static void Init()
        {
            IL.RoR2.CharacterBody.TriggerEnemyDebuffs += CharacterBody_TriggerEnemyDebuffs;
        }

        static void CharacterBody_TriggerEnemyDebuffs(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            MethodInfo splitDebuffInfoListAddMethod = typeof(List<VineOrb.SplitDebuffInformation>).GetMethod(nameof(List<VineOrb.SplitDebuffInformation>.Add));

            if (!c.TryFindNext(out ILCursor[] foundCursors,
                               x => x.MatchCallOrCallvirt<CharacterBody>(nameof(CharacterBody.GetBuffCount)),
                               x => x.MatchCallOrCallvirt(splitDebuffInfoListAddMethod)))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Goto(foundCursors[1].Next, MoveType.Before);
            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<VineOrb.SplitDebuffInformation, CharacterBody, VineOrb.SplitDebuffInformation>>(modifySplitDebuffInfo);

            static VineOrb.SplitDebuffInformation modifySplitDebuffInfo(VineOrb.SplitDebuffInformation splitDebuffInfo, CharacterBody body)
            {
                const float MaxDebuffDuration = 30f;
                if (splitDebuffInfo.duration < MaxDebuffDuration)
                {
                    Inventory inventory = body ? body.inventory : null;

                    ItemQualityCounts triggerEnemyDebuffs = ItemQualitiesContent.ItemQualityGroups.TriggerEnemyDebuffs.GetItemCountsEffective(inventory);

                    if (triggerEnemyDebuffs.TotalQualityCount > 0)
                    {
                        float durationMultiplier = 1f;
                        durationMultiplier += 0.20f * triggerEnemyDebuffs.UncommonCount;
                        durationMultiplier += 0.50f * triggerEnemyDebuffs.RareCount;
                        durationMultiplier += 0.75f * triggerEnemyDebuffs.EpicCount;
                        durationMultiplier += 1.50f * triggerEnemyDebuffs.LegendaryCount;

                        splitDebuffInfo.duration = Mathf.Min(MaxDebuffDuration, splitDebuffInfo.duration * durationMultiplier);
                    }
                }

                return splitDebuffInfo;
            }
        }
    }
}
