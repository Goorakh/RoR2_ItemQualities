using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace ItemQualities
{
    public sealed class InteractOnTimer : MonoBehaviour
    {
        [Min(0f)]
        public float InteractInterval = 1f;

        private IInteractable _interactable;
        private PurchaseInteraction _interactableAsPurchaseInteraction;

        private VendingMachineBehavior _vendingMachineBehavior;

        private Deployable _deployable;

        private float _interactTimer;

        private void Awake()
        {
            _interactable = GetComponent<IInteractable>();
            _interactableAsPurchaseInteraction = _interactable as PurchaseInteraction;

            _vendingMachineBehavior = GetComponent<VendingMachineBehavior>();

            _deployable = GetComponent<Deployable>();
        }

        private void FixedUpdate()
        {
            if (NetworkServer.active)
            {
                _interactTimer += Time.fixedDeltaTime;
                if (_interactTimer >= InteractInterval)
                {
                    _interactTimer -= InteractInterval;
                    triggerInteractServer();
                }
            }
        }

        private void triggerInteractServer()
        {
            if (!NetworkServer.active)
            {
                Log.Warning("Called on client");
                return;
            }

            if (_interactable == null)
                return;

            Interactor interactor = null;
            if (_deployable && _deployable.ownerMaster)
            {
                GameObject deployedToBodyObject = _deployable.ownerMaster.GetBodyObject();
                if (deployedToBodyObject && deployedToBodyObject.TryGetComponent(out Interactor deployedToInteractor))
                {
                    interactor = deployedToInteractor;
                }
            }

            if (interactor)
            {
                CostTypeIndex originalCostType = CostTypeIndex.None;
                bool wasAvailable = false;
                if (_interactableAsPurchaseInteraction)
                {
                    originalCostType = _interactableAsPurchaseInteraction.costType;
                    _interactableAsPurchaseInteraction.costType = CostTypeIndex.None;

                    wasAvailable = _interactableAsPurchaseInteraction.available;
                }

                if (_vendingMachineBehavior)
                {
                    _vendingMachineBehavior.RefreshPurchaseInteractionAvailability();
                }

                if (_interactable.GetInteractability(interactor) == Interactability.Available)
                {
                    interactor.AttemptInteraction(gameObject);
                }

                if (_interactableAsPurchaseInteraction)
                {
                    _interactableAsPurchaseInteraction.costType = originalCostType;
                    _interactableAsPurchaseInteraction.SetAvailable(wasAvailable);
                }
            }
        }
    }
}
