using ItemQualities.ContentManagement;
using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using R2API;
using RoR2;
using RoR2BepInExPack.GameAssetPathsBetter;
using System.Collections;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace ItemQualities.Items
{
    internal static class MinorConstructOnKill
    {
        [ContentInitializer]
        private static IEnumerator LoadContent(ContentInitializerArgs args)
        {
            AsyncOperationHandle<GameObject> goldSiphonTetherVFXLoad = AddressableUtil.LoadTempAssetAsync<GameObject>(RoR2_DLC2.GoldSiphonTetherVFX_prefab);
            goldSiphonTetherVFXLoad.OnSuccess(goldSiphonTetherVFX =>
            {
                GameObject constructBubbleTether = goldSiphonTetherVFX.InstantiateClone("ConstructBubbleTetherVFX", false);
                if (constructBubbleTether.TryGetComponent(out LineRenderer lineRenderer))
                {
                    lineRenderer.widthMultiplier = 0.1f;
                }
                else
                {
                    Log.Error($"{constructBubbleTether} is missing LineRenderer component");
                }

                GameObject attachmentPrefab = args.ContentPack.networkedObjectPrefabs.Find(nameof(ItemQualitiesContent.NetworkedPrefabs.QualityMinorConstructOnKillAttachment));
                if (!attachmentPrefab)
                {
                    Log.Error("Failed to find attachment prefab in content pack");
                    return;
                }

                attachmentPrefab.GetComponent<TetherVfxOrigin>().tetherPrefab = constructBubbleTether;
            });

            return goldSiphonTetherVFXLoad.AsProgressCoroutine(args.ProgressReceiver);
        }
    }

    public sealed class MinorConstructOnKillQualityItemBehavior : QualityItemBodyBehavior
    {
        [ItemGroupAssociation(QualityItemBehaviorUsageFlags.Server)]
        private static ItemQualityGroup GetItemGroup() => ItemQualitiesContent.ItemQualityGroups.MinorConstructOnKill;

        private GameObject _attachmentInstance;

        private void OnEnable()
        {
            _attachmentInstance = Instantiate(ItemQualitiesContent.NetworkedPrefabs.QualityMinorConstructOnKillAttachment);
            _attachmentInstance.GetComponent<NetworkedBodyAttachment>().AttachToGameObjectAndSpawn(gameObject);
        }

        private void OnDisable()
        {
            Destroy(_attachmentInstance);
            _attachmentInstance = null;
        }
    }
}
