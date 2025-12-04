using R2API;
using RoR2;
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

            if (victim.HasBuff(ItemQualitiesContent.Buffs.PersonalShield))
            {
                victim.SetBuffCount(ItemQualitiesContent.Buffs.PersonalShield.buffIndex, Mathf.Max(0, (int)(victim.GetBuffCount(ItemQualitiesContent.Buffs.PersonalShield) - report.damageDealt)));
            }
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

        private static void getStatCoefficients(CharacterBody sender, RecalculateStatsAPI.StatHookEventArgs args)
        {
            if (!sender)
                return;

            args.baseShieldAdd += sender.GetBuffCount(ItemQualitiesContent.Buffs.PersonalShield);
        }
    }
}
