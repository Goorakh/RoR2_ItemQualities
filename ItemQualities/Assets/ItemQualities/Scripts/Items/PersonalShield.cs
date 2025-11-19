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
            GlobalEventManager.OnInteractionsGlobal += OnInteractionsGlobal;
            GlobalEventManager.onServerDamageDealt += onServerDamageDealt;
        }

        private static void onServerDamageDealt(DamageReport report)
        {
            CharacterBody victim = report.victimBody;
            if (!victim)
                return;

            victim.SetBuffCount(ItemQualitiesContent.Buffs.PersonalShield.buffIndex, (int)(victim.GetBuffCount(ItemQualitiesContent.Buffs.PersonalShield) - report.damageDealt));
        }

        private static void OnInteractionsGlobal(Interactor interactor, IInteractable interactable, GameObject @object)
        {
            if (!SharedItemUtils.InteractableIsPermittedForSpawn(interactable))
                return;

            CharacterBody body = interactor.GetComponent<CharacterBody>();
            if (!body)
                return;

            ItemQualityCounts personalShield = ItemQualitiesContent.ItemQualityGroups.PersonalShield.GetItemCountsEffective(body.inventory);
            if (personalShield.TotalQualityCount <= 0)
                return;

            float shieldPerInteract;
            switch (personalShield.HighestQuality)
            {
                case QualityTier.Uncommon:
                    shieldPerInteract = 0.02f;
                    break;
                case QualityTier.Rare:
                    shieldPerInteract = 0.05f;
                    break;
                case QualityTier.Epic:
                    shieldPerInteract = 0.07f;
                    break;
                case QualityTier.Legendary:
                    shieldPerInteract = 0.10f;
                    break;
                default:
                    Log.Error($"Quality tier {personalShield.HighestQuality} is not implemented");
                    return;
            }

            float maxShield = (personalShield.UncommonCount * 0.2f) +
                              (personalShield.RareCount * 0.5f) +
                              (personalShield.EpicCount * 0.9f) +
                              (personalShield.LegendaryCount * 2f);

            body.SetBuffCount(ItemQualitiesContent.Buffs.PersonalShield.buffIndex, (int)Math.Min(body.GetBuffCount(ItemQualitiesContent.Buffs.PersonalShield) + (shieldPerInteract * body.maxHealth), maxShield * body.maxHealth));
        }

        private static void getStatCoefficients(CharacterBody sender, RecalculateStatsAPI.StatHookEventArgs args)
        {
            if (!sender)
                return;

            args.baseShieldAdd += sender.GetBuffCount(ItemQualitiesContent.Buffs.PersonalShield);
        }
    }
}
