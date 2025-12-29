using ItemQualities.Utilities.Extensions;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using System;
using UnityEngine;

namespace ItemQualities.Items
{
    static class BearVoid
    {
        [SystemInitializer]
        static void Init()
        {
            IL.RoR2.HealthComponent.TakeDamageProcess += HealthComponent_TakeDamageProcess;
        }

        static void HealthComponent_TakeDamageProcess(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (!c.TryFindNext(out ILCursor[] foundCursors,
                               x => x.MatchLdsfld(typeof(DLC1Content.Items), nameof(DLC1Content.Items.BearVoid)),
                               x => x.MatchLdsfld(typeof(DLC1Content.Buffs), nameof(DLC1Content.Buffs.BearVoidCooldown)),
                               x => x.MatchCallOrCallvirt<CharacterBody>(nameof(CharacterBody.AddTimedBuff))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Goto(foundCursors[2].Next, MoveType.Before); // call CharacterBody.AddTimedBuff

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<float, HealthComponent, float>>(getCooldown);

            static float getCooldown(float cooldown, HealthComponent healthComponent)
            {
                CharacterBody body = healthComponent ? healthComponent.body : null;
                Inventory inventory = body ? body.inventory : null;
                if (inventory)
                {
                    ItemQualityCounts bearVoid = inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.BearVoid);
                    if (bearVoid.TotalQualityCount > 0)
                    {
                        cooldown *= Mathf.Pow(1f - 0.05f, bearVoid.UncommonCount) *
                                    Mathf.Pow(1f - 0.10f, bearVoid.RareCount) *
                                    Mathf.Pow(1f - 0.20f, bearVoid.EpicCount) *
                                    Mathf.Pow(1f - 0.30f, bearVoid.LegendaryCount);
                    }
                }

                return cooldown;
            }
        }
    }
}
