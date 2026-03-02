using RoR2;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace ItemQualities.Items
{
    public sealed class QualityTierItemBodyBehavior : QualityItemBodyBehavior
    {
        [ItemGroupAssociation(QualityItemBehaviorUsageFlags.Server | QualityItemBehaviorUsageFlags.Client)]
        static ItemQualityGroup GetItemGroup()
        {
            return ItemQualitiesContent.ItemQualityGroups.QualityTier;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static float getScale(QualityTier qualityTier)
        {
            return Mathf.Max(1f, 1.35f + Mathf.Log((int)qualityTier + 1, 4f));
        }

        Collider _collider;
        CameraTargetParams _cameraTargetParams;

        Transform _modelScaleTransform;
        Transform _modelOffsetTransform;

        ItemQualityCounts _previousStack;

        void OnEnable()
        {
            _collider = GetComponent<Collider>();
            _cameraTargetParams = GetComponent<CameraTargetParams>();

            ModelLocator modelLocator = Body.modelLocator;
            if (modelLocator)
            {
                _modelScaleTransform = modelLocator.modelTransform;

                Transform modelOffsetTransform = null;
                if (modelLocator.modelParentTransform)
                {
                    modelOffsetTransform = modelLocator.modelParentTransform;
                }
                else if (_modelScaleTransform && _modelScaleTransform.IsChildOf(modelLocator.transform))
                {
                    modelOffsetTransform = _modelScaleTransform;
                }

                if (modelOffsetTransform != Body.transform)
                {
                    _modelOffsetTransform = modelOffsetTransform;
                }
            }
        }

        void OnDisable()
        {
            setStack(default);
        }

        protected override void OnStacksChanged()
        {
            base.OnStacksChanged();

            setStack(Stacks);
        }

        void setStack(ItemQualityCounts newStack)
        {
            if (_previousStack == newStack)
                return;

            float previousScale = getScale(_previousStack.HighestQuality);
            float newScale = getScale(newStack.HighestQuality);

            float scaleMult = newScale / previousScale;

            _previousStack = newStack;

            if (Mathf.Abs(1f - scaleMult) < Mathf.Epsilon)
                return;

            if (_modelScaleTransform)
            {
                _modelScaleTransform.localScale *= scaleMult;
            }

            if (_modelOffsetTransform)
            {
                _modelOffsetTransform.localPosition *= scaleMult;
            }

            if (Body.characterMotor)
            {
                Body.characterMotor.Motor.SetCapsuleDimensions(Body.characterMotor.capsuleRadius * scaleMult, Body.characterMotor.capsuleHeight * scaleMult, Body.characterMotor.capsuleYOffset * scaleMult);
            }
            else
            {
                switch (_collider)
                {
                    case CapsuleCollider capsuleCollider:
                        capsuleCollider.height *= scaleMult;
                        capsuleCollider.radius *= scaleMult;
                        capsuleCollider.center *= scaleMult;
                        break;
                    case SphereCollider sphereCollider:
                        sphereCollider.radius *= scaleMult;
                        sphereCollider.center *= scaleMult;
                        break;
                    case BoxCollider boxCollider:
                        boxCollider.size *= scaleMult;
                        boxCollider.center *= scaleMult;
                        break;
                }
            }

            if (Body.aimOriginTransform && Body.aimOriginTransform != Body.transform)
            {
                Body.aimOriginTransform.localPosition *= scaleMult;
            }

            if (_cameraTargetParams && _cameraTargetParams.cameraPivotTransform && _cameraTargetParams.cameraPivotTransform != Body.transform)
            {
                _cameraTargetParams.cameraPivotTransform.localPosition *= scaleMult;
            }
        }
    }
}
