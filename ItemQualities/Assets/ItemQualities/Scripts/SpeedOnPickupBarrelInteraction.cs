using ItemQualities.Utilities.Extensions;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace ItemQualities
{
    public sealed class SpeedOnPickupBarrelInteraction : NetworkBehaviour, IInteractable, IDisplayNameProvider
    {
        public GameObject PickupPrefab;

        [SyncVar]
        bool _opened;

        void OnEnable()
        {
            InstanceTracker.Add(this);
            InterfaceInstanceTracker<IInteractable>.Add(this);
        }

        void OnDisable()
        {
            InstanceTracker.Remove(this);
            InterfaceInstanceTracker<IInteractable>.Remove(this);
        }

        public string GetDisplayName()
        {
            return Language.GetString("BARREL_SPEEDONPICKUP_NAME");
        }

        public string GetContextString(Interactor activator)
        {
            return Language.GetString("BARREL_SPEEDONPICKUP_CONTEXT");
        }

        public Interactability GetInteractability(Interactor activator)
        {
            return _opened ? Interactability.Disabled : Interactability.Available;
        }

        [Server]
        public void OnInteractionBegin(Interactor activator)
        {
            if (_opened)
                return;
            
            _opened = true;

            CharacterBody activatorBody = activator ? activator.GetComponent<CharacterBody>() : null;
            Inventory activatorInventory = activatorBody ? activatorBody.inventory : null;

            if (TryGetComponent(out EntityStateMachine stateMachine))
            {
                stateMachine.SetNextState(new EntityStates.Barrel.Opening());
            }

            if (PickupPrefab)
            {
                GameObject pickupObj = Instantiate(PickupPrefab, transform.position + (Vector3.up * 1f), Random.rotationUniform);

                if (pickupObj.TryGetComponent(out TeamFilter teamFilter))
                {
                    teamFilter.teamIndex = activatorBody ? activatorBody.teamComponent.teamIndex : TeamIndex.None;
                }

                ItemQualityCounts speedOnPickup = default;
                if (activatorInventory)
                {
                    speedOnPickup = activatorInventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.SpeedOnPickup);
                }

                if (speedOnPickup.TotalQualityCount <= 0)
                    speedOnPickup.UncommonCount = 1;

                SpeedOnPickupStatsPickup pickupComponent = pickupObj.GetComponentInChildren<SpeedOnPickupStatsPickup>();
                if (pickupComponent)
                {
                    int buffStacks = (3 * speedOnPickup.UncommonCount) +
                                     (5 * speedOnPickup.RareCount) +
                                     (7 * speedOnPickup.EpicCount) +
                                     (10 * speedOnPickup.LegendaryCount);

                    pickupComponent.BuffStacks = buffStacks;
                }

                NetworkServer.Spawn(pickupObj);
            }
        }

        public bool ShouldIgnoreSpherecastForInteractibility(Interactor activator)
        {
            return false;
        }

        public bool ShouldProximityHighlight()
        {
            return true;
        }

        public bool ShouldShowOnScanner()
        {
            return !_opened;
        }

        public bool ShouldSpawnLesserInteractionRewards(Interactor interactor)
        {
            // Our own salvage pickup should replace the standard one, so don't spawn anything
            return false;
        }
    }
}
