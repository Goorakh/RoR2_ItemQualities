using RoR2;
using System;
using UnityEngine;

namespace ItemQualities
{
    [RequireComponent(typeof(NetworkedBodyAttachment))]
    public class BodyAttachmentVFXController : MonoBehaviour, INetworkedBodyAttachmentListener
    {
        [SerializeField]
        InstantiateAddressablePrefab _bodyVFXInstantiator;

        [SerializeField]
        RadiusMode _radiusMode = RadiusMode.BodyRadius;

        [SerializeField]
        [Min(0f)]
        float _radiusMultiplier = 1f;

        NetworkedBodyAttachment _bodyAttachment;

        void Awake()
        {
            _bodyAttachment = GetComponent<NetworkedBodyAttachment>();

            _bodyVFXInstantiator.OnInstantiated += onVFXInstantiated;
        }

        void OnDestroy()
        {
            _bodyVFXInstantiator.OnInstantiated -= onVFXInstantiated;
        }

        void onVFXInstantiated(GameObject vfx)
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
