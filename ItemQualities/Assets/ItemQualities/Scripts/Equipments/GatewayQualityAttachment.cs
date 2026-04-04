using HG;
using ItemQualities.ContentManagement;
using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using R2API;
using RoR2;
using RoR2.Navigation;
using RoR2BepInExPack.GameAssetPathsBetter;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.ResourceManagement.AsyncOperations;

using Random = UnityEngine.Random;

namespace ItemQualities.Equipments
{
    [RequireComponent(typeof(NetworkedBodyAttachment))]
    public sealed class GatewayQualityAttachment : NetworkBehaviour, INetworkedBodyAttachmentListener
    {
        static GameObject _gatewayPickupPrefab;

        [ContentInitializer]
        static IEnumerator LoadContent(ContentIntializerArgs args)
        {
            AsyncOperationHandle<GameObject> elusiveAntlersPickupLoad = AddressableUtil.LoadTempAssetAsync<GameObject>(RoR2_DLC2_Items_SpeedBoostPickup.ElusiveAntlersPickup_prefab);
            elusiveAntlersPickupLoad.OnSuccess(elusiveAntlersPickupPrefab =>
            {
                _gatewayPickupPrefab = elusiveAntlersPickupPrefab.InstantiateClone("QualityGatewayPickup");

                GravitatePickup gravitatePickup = _gatewayPickupPrefab.GetComponentInChildren<GravitatePickup>();
                if (gravitatePickup)
                {
                    Destroy(gravitatePickup.gameObject);
                }

                ElusiveAntlersPickup elusiveAntlersPickup = _gatewayPickupPrefab.GetComponent<ElusiveAntlersPickup>();
                if (elusiveAntlersPickup)
                {
                    Destroy(elusiveAntlersPickup);
                }

                Dictionary<Material, Material> tintMaterialCache = new Dictionary<Material, Material>();
                foreach (Renderer renderer in _gatewayPickupPrefab.GetComponentsInChildren<Renderer>(true))
                {
                    if (!tintMaterialCache.TryGetValue(renderer.sharedMaterial, out Material tintMaterial))
                    {
                        tintMaterial = new Material(renderer.sharedMaterial);
                        tintMaterial.SetColor(ShaderProperties._TintColor, new Color(0.9f, 0.1f, 0.7f, 1f));

                        tintMaterialCache.Add(renderer.sharedMaterial, tintMaterial);
                    }

                    renderer.sharedMaterial = tintMaterial;
                }

                Light light = _gatewayPickupPrefab.GetComponentInChildren<Light>();
                if (light)
                {
                    light.color = new Color32(0xCE, 0x29, 0xCE, 0xFF);
                }

                DestroyOnTimer destroyOnTimer = _gatewayPickupPrefab.EnsureComponent<DestroyOnTimer>();
                destroyOnTimer.duration = 60f;

                _gatewayPickupPrefab.AddComponent<GatewayQualityPickupController>();
            });

            return elusiveAntlersPickupLoad.AsProgressCoroutine(args.ProgressReceiver);
        }

        [SyncVar]
        int _qualityTierInt;
        public QualityTier QualityTier
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (QualityTier)_qualityTierInt - 1;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => _qualityTierInt = (int)value + 1;
        }

        public float SpawnInterval => QualityTier switch
        {
            QualityTier.Uncommon => 10f,
            QualityTier.Rare => 8f,
            QualityTier.Epic => 6f,
            QualityTier.Legendary => 4f,
            _ => float.PositiveInfinity
        };

        public int MaxPickups => QualityTier switch
        {
            QualityTier.Uncommon => 3,
            QualityTier.Rare => 6,
            QualityTier.Epic => 9,
            QualityTier.Legendary => 15,
            _ => 0
        };

        [SyncVar]
        bool _pickupLimitReached;

        int _numActivePickupsServer;

        float _spawnTimer;

        CharacterBody _attachedBody;
        IPhysMotor _attachedBodyMotor;

        void OnEnable()
        {
            InstanceTracker.Add(this);
        }

        void OnDisable()
        {
            InstanceTracker.Remove(this);
        }

        void FixedUpdate()
        {
            if (NetworkServer.active)
            {
                _pickupLimitReached = _numActivePickupsServer >= MaxPickups;
            }

            if (_attachedBody && _attachedBody.hasEffectiveAuthority)
            {
                _spawnTimer += Time.fixedDeltaTime;
                if (_spawnTimer >= SpawnInterval)
                {
                    _spawnTimer = 0f;

                    if (!_pickupLimitReached)
                    {
                        bool foundAnyPosition = false;
                        for (int i = 0; i < 5; i++)
                        {
                            if (tryGetNextPickupPositionAuthority(out Vector3 pickupPosition))
                            {
                                foundAnyPosition = true;
                                createPickupAuthority(pickupPosition);
                                break;
                            }
                        }

                        if (!foundAnyPosition)
                        {
                            Log.Debug("Failed to find spawn position");
                        }
                    }
                    else
                    {
                        Log.Debug("Cannot spawn pickup: limit reached");
                    }
                }
            }
        }

        bool tryGetNextPickupPositionAuthority(out Vector3 pickupPosition)
        {
            if (!_attachedBody)
            {
                pickupPosition = default;
                return false;
            }

            Vector3 bodyForward = _attachedBody.transform.forward;
            if (_attachedBodyMotor != null && _attachedBodyMotor.velocityAuthority.sqrMagnitude > Mathf.Epsilon)
            {
                bodyForward = _attachedBodyMotor.velocityAuthority.normalized;
            }
            else if (_attachedBody.characterDirection)
            {
                if (_attachedBody.characterDirection.moveVector.sqrMagnitude > Mathf.Epsilon)
                {
                    bodyForward = _attachedBody.characterDirection.moveVector.normalized;
                }
                else
                {
                    bodyForward = _attachedBody.characterDirection.forward;
                }
            }

            Vector3 spawnDirection = Quaternion.Euler(Random.Range(-30f, 30f), Random.Range(-45f, 45f), 0f) * bodyForward.XAZ(0f).normalized;

            const float MinDistance = 50f;
            const float MaxDistance = 150f;

            float approximateDistance = Random.Range(MinDistance, MaxDistance);

            Vector3 approximateSpawnPosition = _attachedBody.corePosition + (spawnDirection * approximateDistance);
            if (SceneInfo.instance.groundNodes)
            {
                NodeGraph.NodeIndex spawnNodeIndex = SceneInfo.instance.groundNodes.FindClosestNode(approximateSpawnPosition, _attachedBody.hullClassification, approximateDistance / 2f);
                if (SceneInfo.instance.groundNodes.GetNodePosition(spawnNodeIndex, out Vector3 nodePosition))
                {
                    pickupPosition = nodePosition;
                    return true;
                }
            }

            pickupPosition = default;
            return false;
        }

        void createPickupAuthority(Vector3 pickupPosition)
        {
            if (NetworkServer.active)
            {
                createPickupServer(pickupPosition);
            }
            else
            {
                CmdCreatePickup(pickupPosition);
            }
        }

        [Command]
        void CmdCreatePickup(Vector3 pickupPosition)
        {
            createPickupServer(pickupPosition);
        }

        GameObject createPickupServer(Vector3 pickupPosition)
        {
            if (_pickupLimitReached)
                return null;

            GameObject pickupObj = Instantiate(_gatewayPickupPrefab, pickupPosition, Quaternion.identity);

            if (_attachedBody)
            {
                pickupObj.GetComponent<TeamFilter>().teamIndex = _attachedBody.teamComponent.teamIndex;
            }

            NetworkServer.Spawn(pickupObj);

            _numActivePickupsServer++;

            OnDestroyCallback.AddCallback(pickupObj, _ =>
            {
                _numActivePickupsServer--;
            });

            return pickupObj;
        }

        void INetworkedBodyAttachmentListener.OnAttachedBodyDiscovered(NetworkedBodyAttachment networkedBodyAttachment, CharacterBody attachedBody)
        {
            _attachedBody = attachedBody;
            _attachedBodyMotor = _attachedBody ? _attachedBody.GetComponent<IPhysMotor>() : null;
        }

        public static GatewayQualityAttachment FindAttachmentForBody(CharacterBody body)
        {
            foreach (GatewayQualityAttachment attachment in InstanceTracker.GetInstancesList<GatewayQualityAttachment>())
            {
                if (attachment._attachedBody == body)
                {
                    return attachment;
                }
            }

            return null;
        }
    }
}
