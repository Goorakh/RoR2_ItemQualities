using RoR2;
using System;
using UnityEngine;

namespace ItemQualities
{
    [RequireComponent(typeof(NetworkedBodyAttachment))]
    public sealed class BodyAttachmentVFXController : MonoBehaviour, INetworkedBodyAttachmentListener
    {
        [SerializeField]
        private InstantiateAddressablePrefab _bodyVFXInstantiator;

        [SerializeField]
        private RadiusMode _radiusMode = RadiusMode.BodyRadius;

        [SerializeField]
        [Min(0f)]
        private float _radiusMultiplier = 1f;

        private NetworkedBodyAttachment _bodyAttachment;

        private void Awake()
        {
            _bodyAttachment = GetComponent<NetworkedBodyAttachment>();

            _bodyVFXInstantiator.OnInstantiated += onVFXInstantiated;
        }

        private void OnDestroy()
        {
            _bodyVFXInstantiator.OnInstantiated -= onVFXInstantiated;
        }

        private void onVFXInstantiated(GameObject vfx)
        {
            if (vfx.TryGetComponent(out TemporaryVisualEffect temporaryVisualEffect))
            {
                CharacterBody attachedBody = _bodyAttachment.attachedBody;
                if (attachedBody)
                {
                    float radius = _radiusMode switch
                    {
                        RadiusMode.Constant => 1f,
                        RadiusMode.BodyRadius => attachedBody.radius,
                        RadiusMode.BodyBestFitRadius => attachedBody.bestFitRadius,
                        RadiusMode.BodyBestFitActualRadius => attachedBody.bestFitActualRadius,
                        _ => throw new NotImplementedException($"Radius mode {_radiusMode} is not implemented"),
                    };

                    temporaryVisualEffect.parentTransform = attachedBody.coreTransform;
                    temporaryVisualEffect.visualState = TemporaryVisualEffect.VisualState.Enter;
                    temporaryVisualEffect.healthComponent = attachedBody.healthComponent;
                    temporaryVisualEffect.radius = radius * _radiusMultiplier;

                    if (temporaryVisualEffect.TryGetComponent(out LocalCameraEffect localCameraEffect))
                    {
                        localCameraEffect.targetCharacter = attachedBody.gameObject;
                    }
                }
            }
        }

        void INetworkedBodyAttachmentListener.OnAttachedBodyDiscovered(NetworkedBodyAttachment networkedBodyAttachment, CharacterBody attachedBody)
        {
            _bodyVFXInstantiator.InstantiatePrefab();
        }

        public enum RadiusMode
        {
            Constant,
            BodyRadius,
            BodyBestFitRadius,
            BodyBestFitActualRadius
        }
    }
}
