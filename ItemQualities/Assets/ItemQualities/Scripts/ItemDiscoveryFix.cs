using HG;
using RoR2;
using UnityEngine;

namespace ItemQualities
{
    internal static class ItemDiscoveryFix
    {
        [SystemInitializer]
        private static void Init()
        {
            CharacterMaster.onStartGlobal += onStartGlobal;
        }

        private static void onStartGlobal(CharacterMaster master)
        {
            if (master.playerCharacterMasterController &&
                master.playerCharacterMasterController.networkUser &&
                master.playerCharacterMasterController.networkUser.localUser != null)
            {
                master.gameObject.EnsureComponent<ItemGrantTracker>();
            }
        }

        private sealed class ItemGrantTracker : MonoBehaviour
        {
            private NetworkUser _networkUser;
            private Inventory _inventory;

            private void Awake()
            {
                CharacterMaster master = GetComponent<CharacterMaster>();
                _networkUser = master && master.playerCharacterMasterController ? master.playerCharacterMasterController.networkUser : GetComponent<NetworkUser>();
                _inventory = master ? master.inventory : GetComponent<Inventory>();
            }

            private void OnEnable()
            {
                if (_inventory)
                {
                    _inventory.onItemAddedClient += onItemAddedClient;
                    _inventory.onEquipmentChangedClient += onEquipmentChangedClient;
                }
            }

            private void OnDisable()
            {
                if (_inventory)
                {
                    _inventory.onItemAddedClient -= onItemAddedClient;
                    _inventory.onEquipmentChangedClient -= onEquipmentChangedClient;
                }
            }

            private void onItemAddedClient(ItemIndex itemIndex)
            {
                if (itemIndex != ItemIndex.None)
                {
                    tryDiscoverPickup(PickupCatalog.FindPickupIndex(itemIndex));
                }
            }

            private void onEquipmentChangedClient(EquipmentIndex equipmentIndex, uint equipmentSlot)
            {
                if (equipmentIndex != EquipmentIndex.None)
                {
                    tryDiscoverPickup(PickupCatalog.FindPickupIndex(equipmentIndex));
                }
            }

            private void tryDiscoverPickup(PickupIndex pickupIndex)
            {
                if (pickupIndex != PickupIndex.none && _networkUser)
                {
                    _networkUser.localUser?.userProfile?.DiscoverPickup(pickupIndex);
                }
            }
        }
    }
}
