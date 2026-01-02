using HG;
using ItemQualities.Utilities.Extensions;
using RoR2;
using UnityEngine;

namespace ItemQualities
{
    public class ShrineBossExtraStatsTracker : MonoBehaviour
    {
        [SystemInitializer]
        static void Init()
        {
            On.RoR2.ShrineBossBehavior.Start += ShrineBossBehavior_Start;
        }

        static void ShrineBossBehavior_Start(On.RoR2.ShrineBossBehavior.orig_Start orig, ShrineBossBehavior self)
        {
            orig(self);
            self.gameObject.EnsureComponent<ShrineBossExtraStatsTracker>();
        }

        ShrineBossBehavior _shrineBossBehavior;

        int _baseMaxInteractions;

        void Awake()
        {
            _shrineBossBehavior = GetComponent<ShrineBossBehavior>();
            _baseMaxInteractions = _shrineBossBehavior.maxPurchaseCount;
        }

        void OnEnable()
        {
            refreshMaxPurchases();
            Inventory.onInventoryChangedGlobal += onInventoryChangedGlobal;
        }

        void OnDisable()
        {
            Inventory.onInventoryChangedGlobal -= onInventoryChangedGlobal;
        }

        void onInventoryChangedGlobal(Inventory inventory)
        {
            refreshMaxPurchases();
        }

        void refreshMaxPurchases()
        {
            ItemQualityCounts totalWarbondsCounts = default;

            foreach (PlayerCharacterMasterController playerMaster in PlayerCharacterMasterController.instances)
            {
                if (!playerMaster.isConnected)
                    continue;

                CharacterMaster master = playerMaster ? playerMaster.master : null;
                if (master && master.inventory)
                {
                    totalWarbondsCounts += master.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.BarrageOnBoss);
                }
            }

            int extraInteractionCount = (1 * totalWarbondsCounts.UncommonCount) +
                                        (2 * totalWarbondsCounts.RareCount) +
                                        (3 * totalWarbondsCounts.EpicCount) +
                                        (5 * totalWarbondsCounts.LegendaryCount);

            bool wasBoughtOut = _shrineBossBehavior.purchaseCount >= _shrineBossBehavior.maxPurchaseCount;

            _shrineBossBehavior.maxPurchaseCount = _baseMaxInteractions + extraInteractionCount;

            bool isBoughtOut = _shrineBossBehavior.purchaseCount >= _shrineBossBehavior.maxPurchaseCount;

            if (wasBoughtOut != isBoughtOut)
            {
                if (isBoughtOut)
                {
                    if (_shrineBossBehavior.purchaseInteraction)
                    {
                        _shrineBossBehavior.purchaseInteraction.SetAvailable(false);
                    }

                    _shrineBossBehavior.waitingForRefresh = false;
                }
                else
                {
                    _shrineBossBehavior.refreshTimer = 0f;
                    _shrineBossBehavior.waitingForRefresh = true;
                }

                if (_shrineBossBehavior.symbolTransform)
                {
                    _shrineBossBehavior.symbolTransform.gameObject.SetActive(!isBoughtOut);
                }

                _shrineBossBehavior.CallRpcSetPingable(!isBoughtOut);
            }
        }
    }
}
