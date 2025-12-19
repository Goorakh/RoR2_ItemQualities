using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using R2API;
using RoR2;
using System;
using UnityEngine;

namespace ItemQualities.Items
{
    static class CritDamage
    {
        static readonly DamageColorIndex[] _critDamageColors = new DamageColorIndex[]
        {
            ColorsAPI.RegisterDamageColor(new Color(1f, 0.9f, 0.9f)),
            ColorsAPI.RegisterDamageColor(new Color(1f, 0.7f, 0.7f)),
            ColorsAPI.RegisterDamageColor(new Color(1f, 0.5f, 0.5f)),
            ColorsAPI.RegisterDamageColor(new Color(1f, 0.3f, 0.3f)),
            ColorsAPI.RegisterDamageColor(new Color(1f, 0.1f, 0.1f)),
            ColorsAPI.RegisterDamageColor(new Color(1f, 0.0f, 0.0f))
        };

        [SystemInitializer]
        static void Init()
        {
            IL.RoR2.HealthComponent.TakeDamageProcess += HealthComponent_TakeDamageProcess;
        }

        static void HealthComponent_TakeDamageProcess(ILContext il)
        {
            if (!il.Method.TryFindParameter<DamageInfo>(out ParameterDefinition damageInfoParmemter))
            {
                Log.Error("Failed to find DamageInfo parameter");
                return;
            }

            ILCursor c = new ILCursor(il);

            if (!c.TryFindNext(out ILCursor[] foundCursors,
                               x => x.MatchLdfld<DamageInfo>(nameof(DamageInfo.crit)),
                               x => x.MatchCallOrCallvirt<CharacterBody>("get_" + nameof(CharacterBody.critMultiplier))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Goto(foundCursors[1].Next, MoveType.After); // call CharacterBody.get_critMultiplier

            c.Emit(OpCodes.Ldarg, damageInfoParmemter);
            c.EmitDelegate<Func<float, DamageInfo, float>>(getCritMultiplier);

            static float getCritMultiplier(float critMultiplier, DamageInfo damageInfo)
            {
                if (damageInfo != null && damageInfo.crit)
                {
                    CharacterBody attackerBody = damageInfo.attacker ? damageInfo.attacker.GetComponent<CharacterBody>() : null;
                    if (attackerBody)
                    {
                        Inventory attackerInventory = attackerBody ? attackerBody.inventory : null;

                        ItemQualityCounts critDamage = ItemQualitiesContent.ItemQualityGroups.CritDamage.GetItemCountsEffective(attackerInventory);
                        if (critDamage.TotalQualityCount > 0)
                        {
                            float critStackChance = (20f * critDamage.UncommonCount) +
                                                    (35f * critDamage.RareCount) +
                                                    (50f * critDamage.EpicCount) +
                                                    (75f * critDamage.LegendaryCount);

                            int critStacks = RollUtil.GetOverflowRoll(critStackChance, attackerBody.master, damageInfo.procChainMask.HasProc(ProcType.SureProc));
                            if (critStacks > 0)
                            {
                                critMultiplier = Mathf.Pow(critMultiplier, critStacks + 1);
                                damageInfo.damageColorIndex = _critDamageColors[Mathf.Min(critStacks, _critDamageColors.Length - 1)];
                            }
                        }
                    }
                }

                return critMultiplier;
            }
        }
    }
}
