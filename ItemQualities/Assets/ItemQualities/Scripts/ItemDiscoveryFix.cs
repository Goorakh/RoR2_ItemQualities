using HG;
using RoR2;
using UnityEngine;

namespace ItemQualities
{
    static class ItemDiscoveryFix
    {
        [SystemInitializer]
        static void Init()
        {
            CharacterMaster.onStartGlobal += onStartGlobal;
        }

        static void onStartGlobal(CharacterMaster master)
        {
            if (master.playerCharacterMasterController &&
                master.playerCharacterMasterController.networkUser &&
                master.playerCharacterMasterController.networkUser.localUser != null)
            {
                master.gameObject.EnsureComponent<ItemGrantTracker>();
            }
        }

        class ItemGrantTracker : MonoBehaviour
        {
            NetworkUser _networkUser;
            Inventory _inventory;

            void Awake()
            {
                CharacterMaster master = GetComponent<CharacterMaster>();
                _networkUser = master && master.playerCharacterMasterController ? master.playerCharacterMasterController.networkUser : GetComponent<NetworkUser>();
                _inventory = master ? master.inventory : GetComponent<Inventory>();
            }

            void OnEnable()
            {
                if (_inventory)
                {
                    _inventory.onItemAddedClient += onItemAddedClient;
                    _inventory.onEquipmentChangedClient += onEquipmentChangedClient;
                }
            }

            void OnDisable()
            {
                if (_inventory)
                {
                    _inventory.onItemAddedClient -= onItemAddedClient;
                    _inventory.onEquipmentChangedClient -= onEquipmentChangedClient;
                }
            }

            void onItemAddedClient(ItemIndex itemIndex)
            {
                if (itemIndex != ItemIndex.None)
                {
                    tryDiscoverPickup(PickupCatalog.FindPickupIndex(itemIndex));
                }
            }

            void onEquipmentChangedClient(EquipmentIndex equipmentIndex, uint equipmentSlot)
            {
                if (equipmentIndex != EquipmentIndex.None)
                {
                    tryDiscoverPickup(PickupCatalog.FindPickupIndex(equipmentIndex));
                }
            }

            void tryDiscoverPickup(PickupIndex pickupIndex)
            {
                if (pickupIndex != PickupIndex.none && _networkUser)
                {
                    _networkUser.localUser?.userProfile?.DiscoverPickup(pickupIndex);
                }
            }
        }
    }
}
