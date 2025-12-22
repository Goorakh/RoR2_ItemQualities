using ItemQualities.Utilities.Extensions;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using R2API;
using RoR2;
using System;
using UnityEngine;

namespace ItemQualities.Items
{
    static class PersonalShield
    {
        [SystemInitializer]
        static void Init()
        {
            RecalculateStatsAPI.GetStatCoefficients += getStatCoefficients;

            GlobalEventManager.OnInteractionsGlobal += onInteractGlobal;

            IL.RoR2.HealthComponent.TakeDamageProcess += HealthComponent_TakeDamageProcess;
        }

        static void HealthComponent_TakeDamageProcess(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            int pendingDamageLocalIndex = -1;
            if (!c.TryGotoNext(MoveType.Before,
                               x => x.MatchLdloc(typeof(float), il, out pendingDamageLocalIndex),
                               x => x.MatchLdarg(0),
                               x => x.MatchLdfld<HealthComponent>(nameof(HealthComponent.shield)),
                               x => x.MatchBgtUn(out _)))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Emit(OpCodes.Ldarg_0);
            c.Emit(OpCodes.Ldloc, pendingDamageLocalIndex);
            c.EmitDelegate<Action<HealthComponent, float>>(onShieldDamaged);

            static void onShieldDamaged(HealthComponent healthComponent, float pendingDamage)
            {
                if (!healthComponent || !healthComponent.body)
                    return;

                float shieldDamage = Mathf.Min(pendingDamage, healthComponent.shield);
                if (shieldDamage > 0f)
                {
                    if (healthComponent.body.HasBuff(ItemQualitiesContent.Buffs.PersonalShield))
                    {
                        healthComponent.body.SetBuffCount(ItemQualitiesContent.Buffs.PersonalShield.buffIndex, Mathf.Max(0, (int)(healthComponent.body.GetBuffCount(ItemQualitiesContent.Buffs.PersonalShield) - shieldDamage)));
                    }
                }
            }
        }

        static void onInteractGlobal(Interactor interactor, IInteractable interactable, GameObject @object)
        {
            if (!SharedItemUtils.InteractableIsPermittedForSpawn(interactable))
                return;

            CharacterBody body = interactor.GetComponent<CharacterBody>();
            if (!body)
                return;

            ItemQualityCounts personalShield = ItemQualitiesContent.ItemQualityGroups.PersonalShield.GetItemCountsEffective(body.inventory);
            if (personalShield.TotalQualityCount <= 0)
                return;

            float shieldFractionPerInteract;
            switch (personalShield.HighestQuality)
            {
                case QualityTier.Uncommon:
                    shieldFractionPerInteract = 0.02f;
                    break;
                case QualityTier.Rare:
                    shieldFractionPerInteract = 0.05f;
                    break;
                case QualityTier.Epic:
                    shieldFractionPerInteract = 0.07f;
                    break;
                case QualityTier.Legendary:
                    shieldFractionPerInteract = 0.10f;
                    break;
                default:
                    Log.Error($"Quality tier {personalShield.HighestQuality} is not implemented");
                    return;
            }

            float maxShieldFraction = (personalShield.UncommonCount * 0.2f) +
                                      (personalShield.RareCount * 0.5f) +
                                      (personalShield.EpicCount * 0.9f) +
                                      (personalShield.LegendaryCount * 2f);

            int currentBuffCount = body.GetBuffCount(ItemQualitiesContent.Buffs.PersonalShield);
            int targetBuffCount = Mathf.CeilToInt(currentBuffCount + (shieldFractionPerInteract * body.maxHealth));
            int maxBuffCount = Mathf.CeilToInt(maxShieldFraction * body.maxHealth);
            if (currentBuffCount < targetBuffCount && currentBuffCount < maxBuffCount)
            {
                body.SetBuffCount(ItemQualitiesContent.Buffs.PersonalShield.buffIndex, Mathf.Min(targetBuffCount, maxBuffCount));
            }
        }

        static void getStatCoefficients(CharacterBody sender, RecalculateStatsAPI.StatHookEventArgs args)
        {
            if (!sender)
                return;

            args.baseShieldAdd += sender.GetBuffCount(ItemQualitiesContent.Buffs.PersonalShield);
        }
    }
}
