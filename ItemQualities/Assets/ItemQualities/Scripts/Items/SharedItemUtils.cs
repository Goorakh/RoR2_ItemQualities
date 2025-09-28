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
