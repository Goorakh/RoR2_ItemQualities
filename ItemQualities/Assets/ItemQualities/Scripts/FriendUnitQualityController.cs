using EntityStates;
using EntityStates.FriendUnit;
using HG;
using RoR2;
using UnityEngine;

namespace ItemQualities
{
    public sealed class FriendUnitQualityController : MonoBehaviour
    {
        static readonly string _qualityInteractContextToken = "FRIENDUNIT_QUALITY_PET_CONTEXT";

        [SystemInitializer]
        static void Init()
        {
            if (DLC3Content.BodyPrefabs.FriendUnitBody)
            {
                DLC3Content.BodyPrefabs.FriendUnitBody.gameObject.EnsureComponent<FriendUnitQualityController>();
                DLC3Content.BodyPrefabs.FriendUnitBody.gameObject.EnsureComponent<InteractionProcFilter>();
            }
            else
            {
                Log.Error("Failed to add component to body prefab");
            }
        }

        CharacterBody _body;

        GenericInteraction _genericInteraction;

        FriendUnitController _friendUnitController;

        EntityStateMachine _bodyStateMachine;

        CharacterMaster _ownerMaster;

        bool _qualityBehaviorActive;

        string _defaultInteractContextToken;

        public bool IsQualityBehaviorActive => _qualityBehaviorActive;

        public InteractionProcFilter InteractionProcFilter { get; private set; }

        void Awake()
        {
            _body = GetComponent<CharacterBody>();
            _genericInteraction = GetComponent<GenericInteraction>();
            _friendUnitController = GetComponent<FriendUnitController>();
            InteractionProcFilter = GetComponent<InteractionProcFilter>();

            _bodyStateMachine = EntityStateMachine.FindByCustomName(gameObject, "Body");
        }

        void Start()
        {
            if (_body.master)
            {
                _body.master.minionOwnership.onOwnerDiscovered += setOwnerMaster;
                _body.master.minionOwnership.onOwnerLost += setOwnerMaster;

                setOwnerMaster(_body.master.minionOwnership.ownerMaster);
            }
        }

        void OnDestroy()
        {
            if (_body.master)
            {
                _body.master.minionOwnership.onOwnerDiscovered -= setOwnerMaster;
                _body.master.minionOwnership.onOwnerLost -= setOwnerMaster;
            }

            setOwnerMaster(null);
        }

        void setOwnerMaster(CharacterMaster ownerMaster)
        {
            if (_ownerMaster == ownerMaster)
                return;

            if (_ownerMaster)
            {
                _ownerMaster.inventory.onInventoryChanged -= onOwnerInventoryChanged;
            }

            _ownerMaster = ownerMaster;
            onOwnerInventoryChanged();

            if (_ownerMaster)
            {
                _ownerMaster.inventory.onInventoryChanged += onOwnerInventoryChanged;
            }
        }

        void onOwnerInventoryChanged()
        {
            ItemQualityCounts physicsProjectile = ItemQualitiesContent.ItemQualityGroups.PhysicsProjectile.GetItemCountsEffective(_ownerMaster ? _ownerMaster.inventory : null);
            setQualityBehaviorActive(physicsProjectile.TotalQualityCount > 0);
        }

        void setQualityBehaviorActive(bool active)
        {
            if (_qualityBehaviorActive == active)
                return;

            _qualityBehaviorActive = active;

            if (_qualityBehaviorActive)
            {
                if (_genericInteraction.contextToken != _qualityInteractContextToken)
                {
                    _defaultInteractContextToken = _genericInteraction.contextToken;
                    _genericInteraction.contextToken = _qualityInteractContextToken;
                }

                _genericInteraction.onActivation.AddListener(onQualityInteract);
            }
            else
            {
                if (_genericInteraction.contextToken == _qualityInteractContextToken)
                {
                    _genericInteraction.contextToken = _defaultInteractContextToken;
                }

                _genericInteraction.onActivation.RemoveListener(onQualityInteract);
            }
        }

        void onQualityInteract(Interactor interactor)
        {
            CharacterBody interactorBody = interactor ? interactor.GetComponent<CharacterBody>() : null;
            if (!interactorBody || !interactorBody.inputBank)
                return;

            FriendUnitPunt puntState = new FriendUnitPunt
            {
                Punter = interactorBody.gameObject,
                AimRay = interactorBody.inputBank.GetAimRay()
            };

            if (_bodyStateMachine.SetInterruptState(puntState, InterruptPriority.Frozen))
            {
                _friendUnitController.SetInteractibility(false);
            }
        }
    }
}
