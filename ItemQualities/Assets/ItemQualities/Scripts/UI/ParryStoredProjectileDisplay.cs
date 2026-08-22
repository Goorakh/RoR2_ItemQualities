using ItemQualities.Utilities.Extensions;
using RoR2;
using RoR2.UI;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace ItemQualities.UI
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class ParryStoredProjectileDisplay : MonoBehaviour
    {
        public RawImage ProjectileIcon;

        [Tooltip("Will be enabled/disabled depending on if there is anything to display")]
        public GameObject DisplayRoot;

        public float LowPositionOffset = -20f;

        [NonSerialized]
        public EquipmentIcon ParentEquipmentIcon;

        private float _highPosition;

        private Vector3 _positionVelocity;

        private HUD _hud;

        private MemoizedGetComponentCached<CharacterMasterExtraStatsTracker> _targetMasterStats;

        public new RectTransform transform => base.transform as RectTransform;

        private void Awake()
        {
            _highPosition = transform.anchoredPosition.y;
        }

        private void OnEnable()
        {
            _hud = GetComponentInParent<HUD>();
        }

        private void OnTransformParentChanged()
        {
            _hud = GetComponentInParent<HUD>();
        }

        private void FixedUpdate()
        {
            CharacterMaster targetMaster = _hud ? _hud.targetMaster : null;
            GameObject targetMasterObject = targetMaster ? targetMaster.gameObject : null;

            CharacterMasterExtraStatsTracker targetMasterExtraStats = _targetMasterStats.Get(targetMasterObject);

            bool shouldDisplay = targetMasterExtraStats &&
                                 targetMasterExtraStats.ParryStoredProjectileInfo.ProjectileIndex != -1 &&
                                 targetMaster &&
                                 targetMaster.inventory &&
                                 targetMaster.inventory.currentEquipmentIndex == DLC3Content.Equipment.Parry.equipmentIndex &&
                                 targetMaster.inventory.GetActiveEquipmentQualityTier() > QualityTier.None;

            DisplayRoot.SetActive(shouldDisplay);

            if (shouldDisplay)
            {
                CharacterBody parriedBodyPrefab = BodyCatalog.GetBodyPrefabBodyComponent(targetMasterExtraStats.ParryStoredProjectileInfo.AttackerBodyIndex);

                Texture parriedBodyPortrait = null;

                if (parriedBodyPrefab)
                {
                    parriedBodyPortrait = parriedBodyPrefab.portraitIcon;
                }

                if (!parriedBodyPortrait)
                {
                    parriedBodyPortrait = BodyCatalog.defaultPortraitIcon;
                }

                ProjectileIcon.texture = parriedBodyPortrait;
            }

            bool useHighPosition = ParentEquipmentIcon && ParentEquipmentIcon.stockText && ParentEquipmentIcon.stockText.gameObject.activeInHierarchy;

            Vector2 currentPosition = transform.anchoredPosition;

            Vector3 targetPosition = currentPosition;
            targetPosition.y = useHighPosition ? _highPosition : _highPosition + LowPositionOffset;

            transform.anchoredPosition = Vector3.SmoothDamp(transform.anchoredPosition, targetPosition, ref _positionVelocity, 0.1f);
        }
    }
}
