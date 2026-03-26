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

        float _highPosition;

        Vector3 _positionVelocity;

        HUD _hud;

        MemoizedGetComponentCached<CharacterBodyExtraStatsTracker> _targetBodyExtraStats;

        public new RectTransform transform => base.transform as RectTransform;

        void Awake()
        {
            _highPosition = transform.anchoredPosition.y;
        }

        void OnEnable()
        {
            _hud = GetComponentInParent<HUD>();
        }

        void OnTransformParentChanged()
        {
            _hud = GetComponentInParent<HUD>();
        }

        void FixedUpdate()
        {
            GameObject targetBodyObject = _hud ? _hud.targetBodyObject : null;

            CharacterBodyExtraStatsTracker targetBodyExtraStats = _targetBodyExtraStats.Get(targetBodyObject);
            CharacterBody targetBody = targetBodyExtraStats ? targetBodyExtraStats.Body : null;

            bool shouldDisplay = targetBodyExtraStats &&
                                 targetBodyExtraStats.ParryStoredProjectileIndex != -1 &&
                                 targetBody &&
                                 targetBody.inventory &&
                                 targetBody.inventory.currentEquipmentIndex == DLC3Content.Equipment.Parry.equipmentIndex &&
                                 targetBody.inventory.GetActiveEquipmentQualityTier() > QualityTier.None;

            DisplayRoot.SetActive(shouldDisplay);

            if (shouldDisplay)
            {
                CharacterBody parriedBodyPrefab = BodyCatalog.GetBodyPrefabBodyComponent(targetBodyExtraStats.ParryStoredProjectileAttackerBodyIndex);

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
