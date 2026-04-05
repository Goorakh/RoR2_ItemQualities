using ItemQualities.ContentManagement;
using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using R2API;
using RoR2;
using RoR2BepInExPack.GameAssetPathsBetter;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace ItemQualities.Equipments
{
    static class Gateway
    {
        public static GameObject QualityGatewayPickupTargetIndicatorPrefab { get; private set; }

        [ContentInitializer]
        static IEnumerator LoadContent(ContentIntializerArgs args)
        {
            AsyncOperationHandle<GameObject> lightningIndicatorLoad = AddressableUtil.LoadTempAssetAsync<GameObject>(RoR2_Base_Lightning.LightningIndicator_prefab);
            lightningIndicatorLoad.OnSuccess(lightningIndicatorPrefab =>
            {
                QualityGatewayPickupTargetIndicatorPrefab = lightningIndicatorPrefab.InstantiateClone("QualityGatewayPickupIndicator");

                InputBindingDisplayController inputBindingDisplayController = QualityGatewayPickupTargetIndicatorPrefab.GetComponentInChildren<InputBindingDisplayController>();
                if (inputBindingDisplayController)
                {
                    inputBindingDisplayController.actionName = "Interact";

                    if (inputBindingDisplayController.TryGetComponent(out TMP_Text inputBindingLabel))
                    {
                        inputBindingLabel.color = new Color32(0xEA, 0x2A, 0xAA, 0xFF);
                    }
                }

                foreach (SpriteRenderer renderer in QualityGatewayPickupTargetIndicatorPrefab.GetComponentsInChildren<SpriteRenderer>())
                {
                    renderer.color = new Color32(0xE0, 0x18, 0x83, 0xFF);
                }
            });

            return lightningIndicatorLoad.AsProgressCoroutine(args.ProgressReceiver);
        }

        [SystemInitializer]
        static void Init()
        {
            CharacterBody.onBodyInventoryChangedGlobal += onBodyInventoryChangedGlobal;
        }

        static void onBodyInventoryChangedGlobal(CharacterBody body)
        {
            if (!NetworkServer.active)
                return;

            EquipmentIndex currentEquipmentIndex = body.inventory.currentEquipmentIndex;
            QualityTier currentEquipmentQualityTier = body.inventory.GetActiveEquipmentQualityTier();

            GatewayQualityAttachment qualityAttachment = GatewayQualityAttachment.FindAttachmentForBody(body);
            bool shouldHaveQualityAttachment = currentEquipmentIndex == RoR2Content.Equipment.Gateway.equipmentIndex && currentEquipmentQualityTier != QualityTier.None;

            if (shouldHaveQualityAttachment != qualityAttachment)
            {
                if (shouldHaveQualityAttachment)
                {
                    GameObject qualityAttachmentObj = GameObject.Instantiate(ItemQualitiesContent.NetworkedPrefabs.GatewayQualityAttachment);

                    qualityAttachmentObj.GetComponent<NetworkedBodyAttachment>().AttachToGameObjectAndSpawn(body.gameObject);

                    qualityAttachment = qualityAttachmentObj.GetComponent<GatewayQualityAttachment>();
                }
                else
                {
                    GameObject.Destroy(qualityAttachment.gameObject);
                    qualityAttachment = null;
                }
            }

            if (qualityAttachment)
            {
                qualityAttachment.QualityTier = currentEquipmentQualityTier;
            }
        }
    }
}
