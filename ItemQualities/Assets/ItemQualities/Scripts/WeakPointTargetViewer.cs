using HG;
using ItemQualities.Equipments;
using RoR2;
using RoR2.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ItemQualities
{
    [RequireComponent(typeof(PointViewer))]
    public sealed class WeakPointTargetViewer : MonoBehaviour
    {
        public GameObject VisualizerPrefab;

        private PointViewer _pointViewer;

        private HUD _hud;

        private readonly Dictionary<UnityObjectWrapperKey<HurtBox>, GameObject> _visualizersByHurtBox = new Dictionary<UnityObjectWrapperKey<HurtBox>, GameObject>();

        private void Awake()
        {
            _pointViewer = GetComponent<PointViewer>();
        }

        private void OnEnable()
        {
            OnTransformParentChanged();
        }

        private void OnDisable()
        {
            setDisplayedWeakPoints(Array.Empty<HurtBox>());
            _visualizersByHurtBox.Clear();
        }

        private void OnTransformParentChanged()
        {
            _hud = GetComponentInParent<HUD>();
        }

        private void FixedUpdate()
        {
            using var _ = ListPool<HurtBox>.RentCollection(out List<HurtBox> weakPointHurtBoxes);

            if (_hud && _hud.targetMaster)
            {
                foreach (CharacterBodyExtraStatsTracker bodyExtraStats in InstanceTracker.GetInstancesList<CharacterBodyExtraStatsTracker>())
                {
                    if (bodyExtraStats.WeakPointHurtBoxIndex != -1 && bodyExtraStats.Body && bodyExtraStats.Body.hurtBoxGroup)
                    {
                        HurtBox weakPointHurtBox = ArrayUtils.GetSafe(bodyExtraStats.Body.hurtBoxGroup.hurtBoxes, bodyExtraStats.WeakPointHurtBoxIndex);
                        if (weakPointHurtBox &&
                            weakPointHurtBox.healthComponent &&
                            weakPointHurtBox.healthComponent.alive &&
                            FriendlyFireManager.ShouldDirectHitProceed(weakPointHurtBox.healthComponent, _hud.targetMaster.teamIndex) &&
                            weakPointHurtBox.healthComponent.body != _hud.targetMaster.GetBody())
                        {
                            weakPointHurtBoxes.Add(weakPointHurtBox);
                        }
                    }
                }
            }

            setDisplayedWeakPoints(weakPointHurtBoxes);
        }

        private void setDisplayedWeakPoints(IReadOnlyList<HurtBox> newWeakPoints)
        {
            using var _ = ListPool<UnityObjectWrapperKey<HurtBox>>.RentCollection(out List<UnityObjectWrapperKey<HurtBox>> weakPointsToRemove);

            foreach (UnityObjectWrapperKey<HurtBox> weakPointWrapper in _visualizersByHurtBox.Keys)
            {
                HurtBox weakPoint = weakPointWrapper;
                if (!weakPoint || !newWeakPoints.Contains(weakPoint))
                {
                    weakPointsToRemove.Add(weakPointWrapper);
                }
            }

            foreach (UnityObjectWrapperKey<HurtBox> weakPoint in weakPointsToRemove)
            {
                if (_visualizersByHurtBox.Remove(weakPoint, out GameObject indicatorInstance))
                {
                    _pointViewer.RemoveElement(indicatorInstance);
                }
            }

            foreach (HurtBox weakPoint in newWeakPoints)
            {
                if (!_visualizersByHurtBox.ContainsKey(weakPoint))
                {
                    _visualizersByHurtBox.Add(weakPoint, _pointViewer.AddElement(new PointViewer.AddElementRequest
                    {
                        elementPrefab = VisualizerPrefab,
                        target = weakPoint.transform,
                        targetWorldVerticalOffset = 0f,
                        targetWorldRadius = CritOnUse.WeakPointRadius,
                        scaleWithDistance = true,
                    }));
                }
            }
        }
    }
}
