using HG.Coroutines;
using ItemQualities.ContentManagement;
using ItemQualities.Items;
using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using R2API;
using RoR2;
using RoR2BepInExPack.GameAssetPathsBetter;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace ItemQualities
{
    public sealed class ParentEggSunHandler : MonoBehaviour
    {
        public static GameObject sunPrefab;
        public CharacterBody owner;

        private CharacterBody _body;
        private GameObject _sunInstance;


        [ContentInitializer]
        private static IEnumerator LoadContent(ContentInitializerArgs args)
        {
            AsyncOperationHandle<GameObject> GrandParentSunLoad = AddressableUtil.LoadTempAssetAsync<GameObject>(RoR2_Base_Grandparent.GrandParentSun_prefab);

            ParallelProgressCoroutine prefabsLoadCoroutine = new ParallelProgressCoroutine(args.ProgressReceiver);
            prefabsLoadCoroutine.Add(GrandParentSunLoad);

            yield return prefabsLoadCoroutine;

            if (GrandParentSunLoad.Status != AsyncOperationStatus.Succeeded || !GrandParentSunLoad.Result)
            {
                Log.Error($"Failed to load missile projectile prefab: {GrandParentSunLoad.OperationException}");
                yield break;
            }

            sunPrefab = GrandParentSunLoad.Result.InstantiateClone("miniSun");
            sunPrefab.transform.localScale = Vector3.one / 2f;
            sunPrefab.AddComponent<VisualRadiusHandler>();

            Transform pointLight = sunPrefab.transform.Find("VfxRoot/LightSpinner/LightSpinner/Point Light");
            if (pointLight && pointLight.TryGetComponent(out Light light))
            {
                light.range = 100;
                light.intensity = 0.2f;
            }

            args.ContentPack.networkedObjectPrefabs.Add(sunPrefab);
        }

        private void OnDestroy()
        {
            Destroy(_sunInstance);
        }

        private void Start()
        {
            if (!TryGetComponent(out _body))
            {
                Destroy(gameObject);
                return;
            }

            _sunInstance = GameObject.Instantiate(sunPrefab, _body.footPosition + (Vector3.up * ((_body.bestFitActualRadius * 4) + 1)), Quaternion.identity);

            if (_sunInstance.TryGetComponent(out GrandParentSunController sunController))
            {
                sunController.maxDistance = ParentEgg.SunRange(owner);
                if (sunController.ownership)
                {
                    sunController.ownership.ownerObject = owner.gameObject;
                }
                if (sunController.teamFilter)
                {
                    sunController.teamFilter.teamIndex = owner.teamComponent.teamIndex;
                }
                sunController.bullseyeSearch.teamMaskFilter = TeamMask.AllExcept(owner.teamComponent.teamIndex);
            }

            NetworkServer.Spawn(_sunInstance);
        }

        private void FixedUpdate()
        {
            if (!_sunInstance)
            {
                Destroy(this);
                return;
            }

            _sunInstance.transform.position = _body.footPosition + (Vector3.up * ((_body.bestFitActualRadius * 4) + 1));

            if (!ParentEgg.allowSun(_body))
            {
                if (_sunInstance.TryGetComponent(out EntityStateMachine entityStateMachine))
                {
                    entityStateMachine.SetNextState(new EntityStates.GrandParentSun.GrandParentSunDeath());
                }
            }
        }

        private sealed class VisualRadiusHandler : MonoBehaviour
        {
            private void Awake()
            {
                if (TryGetComponent(out GenericOwnership ownership))
                {
                    ownership.onOwnerChanged += OwnerChanged;
                }
            }

            private void OwnerChanged(GameObject owner)
            {
                if (owner.TryGetComponent(out CharacterBody body))
                {
                    int maxDistance = ParentEgg.SunRange(body);
                    Transform visualRadius = transform.Find("VfxRoot/Mesh/AreaIndicator");
                    if (visualRadius)
                    {
                        visualRadius.localScale = Vector3.one * (maxDistance * 2);
                    }
                }
            }
        }
    }
}
