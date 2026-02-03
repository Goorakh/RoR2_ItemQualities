using RoR2;
using RoR2.ContentManagement;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Networking;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace ItemQualities
{
    [RequireComponent(typeof(NetworkedBodyAttachment))]
    public sealed class RepeatCleanseAttachment : MonoBehaviour
    {
        public bool CleanseDebuffs;
        public bool CleanseBuffs;
        public bool CleanseCooldownBuffs;
        public bool CleanseDots;
        public bool CleanseStun;
        public bool CleanseNearbyProjectiles;

        [Min(0f)]
        public float CleanseInterval = 0.2f;

        public int CleansesRemaining;

        public AssetReferenceGameObject CleanseEffectPrefabReference = new AssetReferenceGameObject(string.Empty);

        [Min(0f)]
        public float CleanseEffectInterval = 0.5f;

        NetworkedBodyAttachment _bodyAttachment;

        float _cleanseTimer = 0f;
        float _cleanseEffectTimer = 0f;

        AsyncOperationHandle<GameObject> _cleanseEffectPrefabLoad;

        void Awake()
        {
            _bodyAttachment = GetComponent<NetworkedBodyAttachment>();

            if (NetworkClient.active)
            {
                _cleanseEffectPrefabLoad = AssetAsyncReferenceManager<GameObject>.LoadAsset(CleanseEffectPrefabReference);
            }
        }

        void OnEnable()
        {
            InstanceTracker.Add(this);
        }

        void OnDisable()
        {
            InstanceTracker.Remove(this);
        }

        void OnDestroy()
        {
            AssetAsyncReferenceManager<GameObject>.UnloadAsset(CleanseEffectPrefabReference);
            _cleanseEffectPrefabLoad = default;
        }

        void FixedUpdate()
        {
            if (NetworkClient.active)
            {
                _cleanseEffectTimer += Time.fixedDeltaTime;
                if (_cleanseEffectTimer >= CleanseEffectInterval)
                {
                    _cleanseEffectTimer -= CleanseEffectInterval;
                    spawnCleanseEffect();
                }
            }

            if (NetworkServer.active)
            {
                _cleanseTimer += Time.fixedDeltaTime;
                if (_cleanseTimer >= CleanseInterval)
                {
                    _cleanseTimer -= CleanseInterval;
                    doCleanse();

                    CleansesRemaining--;
                    if (CleansesRemaining <= 0)
                    {
                        Destroy(gameObject);
                    }
                }
            }
        }

        void doCleanse()
        {
            CharacterBody body = _bodyAttachment.attachedBody;
            if (!body)
                return;

            CleanseSystem.CleanseArgs args = new CleanseSystem.CleanseArgs
            {
                characterBody = body,
                removeDebuffs = CleanseDebuffs,
                removeBuffs = CleanseBuffs,
                removeCooldownBuffs = CleanseCooldownBuffs,
                removeDots = CleanseDots,
                removeStun = CleanseStun,
                removeNearbyProjectiles = CleanseNearbyProjectiles,
            };

            CleanseSystem.CleanseBodyServer(args.characterBody, args);
        }

        void spawnCleanseEffect()
        {
            CharacterBody body = _bodyAttachment.attachedBody;
            if (!body)
                return;

            if (_cleanseEffectPrefabLoad.IsValid())
            {
                EffectData effectData = new EffectData
                {
                    origin = body.corePosition
                };

                effectData.SetNetworkedObjectReference(body.gameObject);

                EffectManager.SpawnEffect(_cleanseEffectPrefabLoad.WaitForCompletion(), effectData, false);
            }
        }

        public static RepeatCleanseAttachment FindCleanseAttachmentForBody(CharacterBody body)
        {
            if (body)
            {
                foreach (RepeatCleanseAttachment attachment in InstanceTracker.GetInstancesList<RepeatCleanseAttachment>())
                {
                    if (attachment._bodyAttachment.attachedBody == body)
                    {
                        return attachment;
                    }
                }
            }

            return null;
        }
    }
}
