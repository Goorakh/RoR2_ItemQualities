using HG;
using ItemQualities.Utilities.Extensions;
using RoR2;
using System;
using UnityEngine;

namespace ItemQualities.Items
{
    public sealed class TPHealingNovaHoldoutZoneController : MonoBehaviour
    {
        [SystemInitializer]
        static void Init()
        {
            On.RoR2.HoldoutZoneController.Awake += HoldoutZoneController_Awake;
        }

        static void HoldoutZoneController_Awake(On.RoR2.HoldoutZoneController.orig_Awake orig, HoldoutZoneController self)
        {
            orig(self);
            self.EnsureComponent<TPHealingNovaHoldoutZoneController>();
        }

        HoldoutZoneController _holdoutZoneController;

        void Awake()
        {
            _holdoutZoneController = GetComponent<HoldoutZoneController>();
        }

        void OnEnable()
        {
            _holdoutZoneController.calcChargeRate += applyRate;
        }

        void OnDisable()
        {
            _holdoutZoneController.calcChargeRate -= applyRate;
        }

        void applyRate(ref float rate)
        {
            if (!_holdoutZoneController)
                return;

            float rateMultiplier = 0;
            foreach (TeamComponent teamComponent in TeamComponent.GetTeamMembers(_holdoutZoneController.chargingTeam))
            {
                if (!teamComponent.body || !teamComponent.body.isPlayerControlled || teamComponent.body.isRemoteOp || !teamComponent.body.inventory)
                    continue;

                if (HoldoutZoneController.IsBodyInChargingRadius(_holdoutZoneController, transform.position, MathF.Pow(_holdoutZoneController.currentRadius, 2), teamComponent.body))
                {
                    ItemQualityCounts tpHealingNova = teamComponent.body.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.TPHealingNova);

                    rateMultiplier += (tpHealingNova.UncommonCount * 0.4f) +
                                      (tpHealingNova.RareCount * 0.8f) +
                                      (tpHealingNova.EpicCount * 1.2f) +
                                      (tpHealingNova.LegendaryCount * 1.6f);
                }
            }

            rate *= 1f + (rateMultiplier / HoldoutZoneController.CountLivingPlayers(_holdoutZoneController.chargingTeam));
        }
    }
}
