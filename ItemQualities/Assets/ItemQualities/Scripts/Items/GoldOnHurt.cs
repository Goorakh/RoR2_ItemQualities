using ItemQualities.Utilities.Extensions;
using RoR2;
using RoR2BepInExPack.GameAssetPaths.Version_1_35_0;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Networking;

namespace ItemQualities.Items
{
    static class GoldOnHurt
    {
        static GameObject _goldPackPrefab;

        [SystemInitializer]
        static void Init()
        {
            Addressables.LoadAssetAsync<GameObject>(RoR2_Base_BonusGoldPackOnKill.BonusMoneyPack_prefab).OnSuccess(prefab =>
            {
                _goldPackPrefab = prefab;
            });

            GlobalEventManager.OnInteractionsGlobal += onInteractGlobal;
        }

        static void onInteractGlobal(Interactor interactor, IInteractable interactable, GameObject interactableObject)
        {
            if (!NetworkServer.active)
                return;

            CharacterBody interactorBody = interactor ? interactor.GetComponent<CharacterBody>() : null;
            Inventory interactorInventory = interactorBody ? interactorBody.inventory : null;
            if (!interactorInventory)
                return;

            ItemQualityCounts goldOnHurt = interactorInventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.GoldOnHurt);
            if (goldOnHurt.TotalQualityCount > 0)
            {
                if (interactable is BarrelInteraction barrelInteraction)
                {
                    if (_goldPackPrefab)
                    {
                        Vector3 spawnPosition = interactableObject.transform.position;
                        if (interactableObject.TryGetComponent(out ModelLocator modelLocator))
                        {
                            ChildLocator modelChildLocator = modelLocator.modelChildLocator;
                            if (modelChildLocator)
                            {
                                Transform fireworkOrigin = modelChildLocator.FindChild("FireworkOrigin");
                                if (fireworkOrigin)
                                {
                                    spawnPosition = fireworkOrigin.position;
                                }
                            }
                        }

                        GameObject moneyPackObj = GameObject.Instantiate(_goldPackPrefab, spawnPosition, Quaternion.identity);

                        MoneyPickup moneyPickup = moneyPackObj.GetComponentInChildren<MoneyPickup>();
                        if (moneyPickup)
                        {
                            int bonusMoney = (25 * goldOnHurt.UncommonCount) +
                                             (50 * goldOnHurt.RareCount) +
                                             (75 * goldOnHurt.EpicCount) +
                                             (100 * goldOnHurt.LegendaryCount);

                            moneyPickup.baseGoldReward = bonusMoney;
                        }

                        if (interactorBody)
                        {
                            if (moneyPackObj.TryGetComponent(out TeamFilter teamFilter))
                            {
                                teamFilter.teamIndex = interactorBody.teamComponent.teamIndex;
                            }
                        }

                        NetworkServer.Spawn(moneyPackObj);
                    }
                }
            }
        }
    }
}
