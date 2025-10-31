using ItemQualities.ContentManagement;
using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using R2API;
using RoR2;
using RoR2BepInExPack.GameAssetPathsBetter;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace ItemQualities.Items
{
    static class WardOnLevel
    {
        static GameObject _wardTemporaryPrefab;

        [ContentInitializer]
        static IEnumerator LoadContent(ContentIntializerArgs args)
        {
            AsyncOperationHandle<GameObject> warbannerWardLoad = AddressableUtil.LoadTempAssetAsync<GameObject>(RoR2_Base_WardOnLevel.WarbannerWard_prefab);
            warbannerWardLoad.OnSuccess(warbannerWard =>
            {
                GameObject warbannerWardTemporaryObj = warbannerWard.InstantiateClone("WarbannerWardTemporary");

                GenericDurationComponent durationComponent = warbannerWardTemporaryObj.AddComponent<GenericDurationComponent>();

                BuffWard buffWard = warbannerWardTemporaryObj.GetComponent<BuffWard>();
                buffWard.expires = true;
                buffWard.expireDuration = 15f;

                durationComponent.BuffWard = buffWard;

                Transform modelTransform = warbannerWardTemporaryObj.transform.Find("mdlWarbanner");
                if (modelTransform)
                {
                    GameObject modelRootObj = new GameObject("ModelRoot");
                    modelRootObj.transform.SetParent(modelTransform, false);
                    modelRootObj.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                    modelRootObj.transform.localScale = Vector3.one;

                    for (int i = modelTransform.childCount - 1; i >= 0; i--)
                    {
                        Transform modelChild = modelTransform.GetChild(i);
                        if (modelChild != modelRootObj.transform)
                        {
                            modelChild.SetParent(modelRootObj.transform, true);
                        }
                    }

                    BeginRapidlyActivatingAndDeactivating endBlinkController = warbannerWardTemporaryObj.AddComponent<BeginRapidlyActivatingAndDeactivating>();
                    endBlinkController.delayBeforeBeginningBlinking = 9f;
                    endBlinkController.blinkFrequency = 10f;
                    endBlinkController.blinkingRootObject = modelRootObj;

                    durationComponent.BlinkController = endBlinkController;
                    durationComponent.BlinkDuration = 1f;
                }
                else
                {
                    Log.Error($"Failed to find warbanner model root on {Util.GetGameObjectHierarchyName(warbannerWardTemporaryObj)}");
                }

                args.ContentPack.networkedObjectPrefabs.Add(warbannerWardTemporaryObj);
                _wardTemporaryPrefab = warbannerWardTemporaryObj;
            });

            return warbannerWardLoad.AsProgressCoroutine(args.ProgressReceiver);
        }

        [SystemInitializer]
        static void Init()
        {
            GlobalEventManager.OnInteractionsGlobal += onInteractionsGlobal;
        }

        static void onInteractionsGlobal(Interactor interactor, IInteractable interactable, GameObject interactableObject)
        {
            if (!NetworkServer.active)
                return;

            if (!SharedItemUtils.InteractableIsPermittedForSpawn(interactable))
                return;

            CharacterBody interactorBody = interactor ? interactor.GetComponent<CharacterBody>() : null;
            TeamIndex interactorTeam = interactorBody && interactorBody.teamComponent ? interactorBody.teamComponent.teamIndex : TeamIndex.None;

            Inventory interactorInventory = interactorBody ? interactorBody.inventory : null;

            ItemQualityCounts wardOnLevel = default;
            if (interactorInventory)
            {
                wardOnLevel = ItemQualitiesContent.ItemQualityGroups.WardOnLevel.GetItemCounts(interactorInventory);
            }

            float wardDuration = (3f * wardOnLevel.UncommonCount) +
                                 (8f * wardOnLevel.RareCount) +
                                 (15f * wardOnLevel.EpicCount) +
                                 (30f * wardOnLevel.LegendaryCount);

            if (wardDuration > 0f)
            {
                Vector3 wardSpawnPosition = interactorBody ? interactorBody.footPosition : interactableObject.transform.position;

                GameObject temporaryWardObj = GameObject.Instantiate(_wardTemporaryPrefab, wardSpawnPosition, Quaternion.identity);

                TeamFilter teamFilter = temporaryWardObj.GetComponent<TeamFilter>();
                teamFilter.teamIndex = interactorTeam;

                BuffWard buffWard = temporaryWardObj.GetComponent<BuffWard>();
                buffWard.Networkradius = 8f + (8f * wardOnLevel.TotalCount);

                GenericDurationComponent durationComponent = temporaryWardObj.GetComponent<GenericDurationComponent>();
                durationComponent.Duration = wardDuration;

                NetworkServer.Spawn(temporaryWardObj);
            }
        }
    }
}
