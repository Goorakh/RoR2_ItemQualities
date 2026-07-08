using ItemQualities.Utilities.Extensions;
using RoR2;
using UnityEngine;

namespace ItemQualities
{
    public sealed class ShrineChanceExtraStatsTracker : MonoBehaviour
    {
        [SystemInitializer]
        private static void Init()
        {
            On.RoR2.ShrineChanceBehavior.Awake += ShrineChanceBehavior_Awake;
        }

        private static void ShrineChanceBehavior_Awake(On.RoR2.ShrineChanceBehavior.orig_Awake orig, ShrineChanceBehavior self)
        {
            self.gameObject.AddComponent<ShrineChanceExtraStatsTracker>();
            orig(self);
        }

        private ShrineChanceBehavior _shrineChanceBehavior;

        private int _baseMaxInteractions;

        private void Awake()
        {
            _shrineChanceBehavior = GetComponent<ShrineChanceBehavior>();
            _baseMaxInteractions = _shrineChanceBehavior.maxPurchaseCount;
        }

        private void OnEnable()
        {
            refreshMaxPurchases();
            Inventory.onInventoryChangedGlobal += onInventoryChangedGlobal;
        }

        private void OnDisable()
        {
            Inventory.onInventoryChangedGlobal -= onInventoryChangedGlobal;
        }

        private void onInventoryChangedGlobal(Inventory inventory)
        {
            refreshMaxPurchases();
        }

        private void refreshMaxPurchases()
        {
            ItemQualityCounts totalChanceDollCounts = default;

            foreach (PlayerCharacterMasterController playerMaster in PlayerCharacterMasterController.instances)
            {
                if (!playerMaster.isConnected)
                    continue;

                CharacterMaster master = playerMaster ? playerMaster.master : null;
                if (master && master.inventory)
                {
                    totalChanceDollCounts += master.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.ExtraShrineItem);
                }
            }

            int extraInteractionCount = (1 * totalChanceDollCounts.UncommonCount) +
                                        (2 * totalChanceDollCounts.RareCount) +
                                        (3 * totalChanceDollCounts.EpicCount) +
                                        (5 * totalChanceDollCounts.LegendaryCount);

            bool wasBoughtOut = _shrineChanceBehavior.successfulPurchaseCount >= _shrineChanceBehavior.maxPurchaseCount;

            _shrineChanceBehavior.maxPurchaseCount = _baseMaxInteractions + extraInteractionCount;

            bool isBoughtOut = _shrineChanceBehavior.successfulPurchaseCount >= _shrineChanceBehavior.maxPurchaseCount;

            if (wasBoughtOut != isBoughtOut)
            {
                if (isBoughtOut)
                {
                    if (_shrineChanceBehavior.purchaseInteraction)
                    {
                        _shrineChanceBehavior.purchaseInteraction.SetAvailable(false);
                    }

                    _shrineChanceBehavior.waitingForRefresh = false;
                }
                else
                {
                    _shrineChanceBehavior.refreshTimer = 0f;
                    _shrineChanceBehavior.waitingForRefresh = true;
                }

                if (_shrineChanceBehavior.symbolTransform)
                {
                    _shrineChanceBehavior.symbolTransform.gameObject.SetActive(!isBoughtOut);
                }

                _shrineChanceBehavior.CallRpcSetPingable(!isBoughtOut);
            }
        }
    }
}
