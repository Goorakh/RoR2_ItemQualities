using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using System;
using UnityEngine;

namespace ItemQualities.Items
{
    static class SpeedBoostPickup
    {
        [SystemInitializer]
        static void Init()
        {
            IL.RoR2.CharacterBody.GetElusiveAntlersCurrentMaxStack += ItemHooks.CombineGroupedItemCountsPatch;
            IL.RoR2.ElusiveAntlersBehavior.FixedUpdate += ItemHooks.CombineGroupedItemCountsPatch;
            IL.RoR2.ElusiveAntlersPickup.OnTriggerStay += ElusiveAntlersPickup_OnTriggerStay;
        }

        static void ElusiveAntlersPickup_OnTriggerStay(ILContext il)
        {
            ItemHooks.CombineGroupedItemCountsPatch(il);

            ILCursor c = new ILCursor(il);

            if (!c.TryFindNext(out ILCursor[] foundCursors,
                               x => x.MatchLdsfld(typeof(DLC2Content.Buffs), nameof(DLC2Content.Buffs.ElusiveAntlersBuff)),
                               x => x.MatchCallOrCallvirt<CharacterBody>(nameof(CharacterBody.AddTimedBuff))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Goto(foundCursors[1].Next, MoveType.Before); // call CharacterBody.AddTimedBuff

            int targetBodyLocalIndex = -1;
            if (c.TryFindPrev(out _,
                              x => x.MatchLdloc(typeof(CharacterBody), il, out targetBodyLocalIndex)))
            {
                c.Emit(OpCodes.Ldloc, targetBodyLocalIndex);
            }
            else
            {
                Log.Warning("Failed to find targetBody variable");

                if (!il.Method.TryFindParameter<Collider>(out ParameterDefinition otherColliderParameter))
                {
                    Log.Error("Failed to find Collider parameter");
                    return;
                }

                c.Emit(OpCodes.Ldarg, otherColliderParameter);
                c.EmitDelegate<Func<Collider, CharacterBody>>(getTargetBody);

                static CharacterBody getTargetBody(Collider collider)
                {
                    if (!collider)
                        return null;

                    if (collider.TryGetComponent(out CharacterBody body))
                        return body;

                    if (collider.TryGetComponent(out EntityLocator entityLocator) &&
                        entityLocator.entity &&
                        entityLocator.entity.TryGetComponent(out CharacterBody entityBody))
                    {
                        return entityBody;
                    }

                    return null;
                }
            }

            c.EmitDelegate<Action<CharacterBody>>(beforeGiveAntlerBuff);

            static void beforeGiveAntlerBuff(CharacterBody body)
            {
                if (!body)
                    return;

                ItemQualityCounts speedBoostPickup = ItemQualitiesContent.ItemQualityGroups.SpeedBoostPickup.GetItemCounts(body.inventory);
                if (speedBoostPickup.TotalQualityCount > 0 &&
                    body.GetBuffCount(DLC2Content.Buffs.ElusiveAntlersBuff) >= Mathf.Min(6, body.GetElusiveAntlersCurrentMaxStack()))
                {
                    float invisibilityDuration = (1f * speedBoostPickup.UncommonCount) + 
                                                 (3f * speedBoostPickup.RareCount) +
                                                 (6f * speedBoostPickup.EpicCount) +
                                                 (8f * speedBoostPickup.LegendaryCount);

                    if (invisibilityDuration > 0f)
                    {
                        body.AddTimedBuff(RoR2Content.Buffs.Cloak, invisibilityDuration);
                    }
                }
            }
        }
    }
}
