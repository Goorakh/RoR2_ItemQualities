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
        static float getScale(in ItemQualityCounts qualities)
        {
            return Mathf.Max(1f, 1.5f + Mathf.Log((int)qualities.HighestQuality + 1, 4f));
        }

        Collider _collider;
        CameraTargetParams _cameraTargetParams;

        ItemQualityCounts _previousStack;

        void OnEnable()
        {
            _collider = GetComponent<Collider>();
            _cameraTargetParams = GetComponent<CameraTargetParams>();
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

            float previousScale = getScale(_previousStack);
            float newScale = getScale(newStack);

            float scaleMult = newScale / previousScale;

            _previousStack = newStack;

            if (Mathf.Abs(1f - scaleMult) < Mathf.Epsilon)
                return;

            Transform modelTransform = Body.modelLocator ? Body.modelLocator.modelTransform : null;
            if (modelTransform)
            {
                modelTransform.localScale *= scaleMult;
                modelTransform.localPosition *= scaleMult;
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

            if (Body.aimOriginTransform)
            {
                Body.aimOriginTransform.localPosition *= scaleMult;
            }

            if (_cameraTargetParams && _cameraTargetParams.cameraPivotTransform)
            {
                _cameraTargetParams.cameraPivotTransform.localPosition *= scaleMult;
            }
        }
    }
}
