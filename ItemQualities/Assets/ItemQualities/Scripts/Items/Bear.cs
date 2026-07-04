using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.Utils;
using RoR2;
using System;

namespace ItemQualities.Items
{
    static class Bear
    {
        [SystemInitializer]
        static void Init()
        {
            IL.RoR2.HealthComponent.TakeDamageProcess += HealthComponent_TakeDamageProcess;
        }

        static void HealthComponent_TakeDamageProcess(ILContext il)
        {
            if (!il.Method.TryFindParameter<DamageInfo>(out ParameterDefinition damageInfoParameter))
            {
                Log.Error("Failed to find DamageInfo parameter");
                return;
            }

            ILCursor c = new ILCursor(il);

            if (!c.TryGotoNext(MoveType.Before,
                               x => x.MatchCallOrCallvirt<IOnIncomingDamageServerReceiver>(nameof(IOnIncomingDamageServerReceiver.OnIncomingDamageServer))))
            {
                Log.Error("Failed to find OnIncomingDamageServer call location");
                return;
            }

            if (!c.TryGotoNext(MoveType.AfterLabel,
                               x => x.MatchLdloc(out _),
                               x => x.MatchLdfld(out FieldReference field) && field?.FieldType?.Is(typeof(DamageInfo)) == true,
                               x => x.MatchLdfld<DamageInfo>(nameof(DamageInfo.rejected))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Emit(OpCodes.Ldarg_0);
            c.Emit(OpCodes.Ldarg, damageInfoParameter);
            c.EmitDelegate<Action<HealthComponent, DamageInfo>>(onIncomingDamageServer);

            static void onIncomingDamageServer(HealthComponent healthComponent, DamageInfo damageInfo)
            {
                CharacterBody body = healthComponent.body;
                Inventory inventory = body ? body.inventory : null;
                if (!inventory)
                    return;

                ItemQualityCounts bear = inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.Bear);
                if (bear.TotalQualityCount > 0 && damageInfo.rejected)
                {
                    bool isInvincible = body.HasBuff(RoR2Content.Buffs.Immune) ||
                                        body.HasBuff(DLC2Content.Buffs.SojournVehicle) ||
                                        body.HasBuff(RoR2Content.Buffs.HiddenInvincibility);

                    if (!isInvincible || damageInfo.IsParried())
                    {
                        float damageFraction = damageInfo.damage / body.healthComponent.fullCombinedHealth;

                        float invincibilityDurationPerPercentDamage = (0.02f * bear.UncommonCount) +
                                                                      (0.05f * bear.RareCount) +
                                                                      (0.1f * bear.EpicCount) +
                                                                      (0.15f * bear.LegendaryCount);

                        int maxDuration = (3 * bear.UncommonCount) +
                                          (6 * bear.RareCount) +
                                          (9 * bear.EpicCount) +
                                          (12 * bear.LegendaryCount);

                        float invincibilityDuration = Math.Min(damageFraction * 100f * invincibilityDurationPerPercentDamage, maxDuration);
                        if (invincibilityDuration >= 1f / 30f)
                        {
                            body.AddTimedBuff(RoR2Content.Buffs.Immune, invincibilityDuration);
                        }
                    }
                }
            }
        }
    }
}
