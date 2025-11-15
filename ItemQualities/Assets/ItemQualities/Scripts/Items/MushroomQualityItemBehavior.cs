using AK.Wwise;
using EntityStates;
using EntityStates.MushroomShield;
using HG;
using HG.Coroutines;
using ItemQualities.ContentManagement;
using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using R2API;
using RoR2;
using RoR2.Projectile;
using RoR2BepInExPack.GameAssetPathsBetter;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace ItemQualities.Items
{
    public sealed class MushroomQualityItemBehavior : MonoBehaviour
    {
        static DeployableSlot _mushroomBubbleDeployableSlot = DeployableSlot.None;

        static GameObject _bubbleShieldPrefab;

        CharacterBody _body;
        GameObject _currentShield;

        static int getMushroomBubbleLimit(CharacterMaster master, int deployableCountMultiplier)
        {
            return 1;
        }

        [SystemInitializer]
        static void Init()
        {
            _mushroomBubbleDeployableSlot = DeployableAPI.RegisterDeployableSlot(getMushroomBubbleLimit);
        }

        [ContentInitializer]
        static IEnumerator LoadContent(ContentIntializerArgs args)
        {
            AsyncOperationHandle<GameObject> bubbleShieldLoad = AddressableUtil.LoadTempAssetAsync<GameObject>(RoR2_Base_Engi.EngiBubbleShield_prefab);
            AsyncOperationHandle<GameObject> engiBodyLoad = AddressableUtil.LoadTempAssetAsync<GameObject>(RoR2_Base_Engi.EngiBody_prefab);

            ParallelProgressCoroutine prefabsLoadCoroutine = new ParallelProgressCoroutine(args.ProgressReceiver);
            prefabsLoadCoroutine.Add(bubbleShieldLoad);
            prefabsLoadCoroutine.Add(engiBodyLoad);

            yield return prefabsLoadCoroutine;

            if (bubbleShieldLoad.Status != AsyncOperationStatus.Succeeded || !bubbleShieldLoad.Result)
            {
                Log.Error($"Failed to load Engie Bubble Shield prefab: {bubbleShieldLoad.OperationException}");
                yield break;
            }

            _bubbleShieldPrefab = bubbleShieldLoad.Result.InstantiateClone("MushroomShield");
            Destroy(_bubbleShieldPrefab.GetComponent<ApplyTorqueOnStart>());
            Destroy(_bubbleShieldPrefab.GetComponent<ProjectileStickOnImpact>());
            Destroy(_bubbleShieldPrefab.GetComponent<ProjectileController>());
            Destroy(_bubbleShieldPrefab.GetComponent<ProjectileDamage>());
            Destroy(_bubbleShieldPrefab.GetComponent<ProjectileNetworkTransform>());
            Destroy(_bubbleShieldPrefab.GetComponent<ProjectileSimple>());
            Destroy(_bubbleShieldPrefab.GetComponent<Rigidbody>());

            EntityStateMachine statemachine = _bubbleShieldPrefab.GetComponent<EntityStateMachine>();
            if (!statemachine)
            {
                Log.Error("Missing EntityStateMachine in MushroomShield");
                yield break;
            }

            SerializableEntityStateType mushroomBubbleDeploy = new SerializableEntityStateType(typeof(MushroomBubbleDeploy));
            statemachine.initialStateType = mushroomBubbleDeploy;
            statemachine.mainStateType = mushroomBubbleDeploy;

            ChildLocator childLocator = _bubbleShieldPrefab.GetComponent<ChildLocator>();
            if (childLocator)
            {
                Transform child = childLocator.FindChild("Bubble");
                if (!child)
                {
                    Log.Error("Failed to find child in MushroomShield");
                    yield break;
                }

                child.gameObject.SetActive(true);
            }

            if (_bubbleShieldPrefab.TryGetComponent(out BeginRapidlyActivatingAndDeactivating blinkController))
            {
                blinkController.enabled = false;
            }

            Deployable deployable = _bubbleShieldPrefab.EnsureComponent<Deployable>();
            deployable.onUndeploy = new UnityEvent();

            _bubbleShieldPrefab.AddComponent<GenericOwnership>();
            _bubbleShieldPrefab.AddComponent<MushroomBubbleController>();

            Bank engiBank = null;
            if (engiBodyLoad.Status == AsyncOperationStatus.Succeeded && engiBodyLoad.Result)
            {
                if (engiBodyLoad.Result.TryGetComponent(out AkBank engiBodyBank))
                {
                    engiBank = engiBodyBank.data;
                }
            }

            if (engiBank != null)
            {
                AkBank shieldBank = _bubbleShieldPrefab.AddComponent<AkBank>();
                shieldBank.data = engiBank;
                shieldBank.triggerList = new List<int> { AkTriggerHandler.ON_ENABLE_TRIGGER_ID };
                shieldBank.unloadTriggerList = new List<int> { AkTriggerHandler.ON_DISABLE_TRIGGER_ID };
            }
            else
            {
                Log.Warning("Failed to load engineer sound bank");
            }

            args.ContentPack.networkedObjectPrefabs.Add(_bubbleShieldPrefab);
        }

        void Awake()
        {
            _body = GetComponent<CharacterBody>();
        }

        void FixedUpdate()
        {
            if (!NetworkServer.active)
                return;

            if (_body && _body.notMovingStopwatch >= 0.25f && _body.healthComponent && _body.healthComponent.alive)
            {
                if (!_currentShield)
                {
                    _currentShield = Instantiate(_bubbleShieldPrefab, transform.position, Quaternion.identity);
                    _currentShield.GetComponent<GenericOwnership>().ownerObject = gameObject;
                    
                    if (_body.master)
                    {
                        Deployable deployable = _currentShield.GetComponent<Deployable>();
                        _body.master.AddDeployable(deployable, _mushroomBubbleDeployableSlot);
                    }

                    NetworkServer.Spawn(_currentShield);
                }
            }
            else
            {
                _currentShield = null;
            }
        }
    }
}
