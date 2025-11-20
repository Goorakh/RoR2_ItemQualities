using RoR2;
using UnityEngine;

namespace ItemQualities.Items
{
    static class SharedItemUtils
    {
        public static bool InteractableIsPermittedForSpawn(IInteractable interactable)
        {
            if (interactable is not MonoBehaviour interactableBehavior || !interactableBehavior)
                return false;

            if (interactableBehavior.TryGetComponent(out InteractionProcFilter interactionProcFilter))
                return interactionProcFilter.shouldAllowOnInteractionBeginProc;

            if (interactableBehavior.TryGetComponent(out PurchaseInteraction purchaseInteraction))
                return !purchaseInteraction.disableSpawnOnInteraction;

            if (interactableBehavior.TryGetComponent(out PowerPedestal powerPedestal))
                return powerPedestal.CanTriggerFireworks;

            if (interactableBehavior.TryGetComponent(out AccessCodesNodeController accessCodesNodeController))
                return accessCodesNodeController.CheckInteractionOrder();

            if (interactableBehavior.GetComponent<DelusionChestController>())
                return !interactableBehavior.TryGetComponent(out PickupPickerController pickupPickerController) || !pickupPickerController.enabled;

            if (interactableBehavior.GetComponent<GenericPickupController>())
                return false;

            if (interactableBehavior.GetComponent<VehicleSeat>())
                return false;

            if (interactableBehavior.GetComponent<NetworkUIPromptController>())
                return false;

            return true;
        }
    }
}
