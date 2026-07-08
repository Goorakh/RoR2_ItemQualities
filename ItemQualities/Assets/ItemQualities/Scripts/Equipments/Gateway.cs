using ItemQualities.ContentManagement;
using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using R2API;
using RoR2;
using RoR2.Navigation;
using RoR2BepInExPack.GameAssetPathsBetter;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace ItemQualities.Equipments
{
    internal static class Gateway
    {
        public static GameObject QualityGatewayPickupTargetIndicatorPrefab { get; private set; }

        [ContentInitializer]
        private static IEnumerator LoadContent(ContentInitializerArgs args)
        {
            AsyncOperationHandle<GameObject> lightningIndicatorLoad = AddressableUtil.LoadTempAssetAsync<GameObject>(RoR2_Base_Lightning.LightningIndicator_prefab);
            lightningIndicatorLoad.OnSuccess(lightningIndicatorPrefab =>
            {
                QualityGatewayPickupTargetIndicatorPrefab = lightningIndicatorPrefab.InstantiateClone("QualityGatewayPickupIndicator", false);

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
        private static void Init()
        {
            CharacterBody.onBodyInventoryChangedGlobal += onBodyInventoryChangedGlobal;

            On.RoR2.EquipmentSlot.FireGateway += EquipmentSlot_FireGateway;
        }

        private static void onBodyInventoryChangedGlobal(CharacterBody body)
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

        private static bool EquipmentSlot_FireGateway(On.RoR2.EquipmentSlot.orig_FireGateway orig, EquipmentSlot self)
        {
            bool success = orig(self);

            if (success)
            {
                QualityTier qualityTier = self.GetCurrentEquipmentActionQualityTier();
                if (qualityTier != QualityTier.None)
                {
                    int numPickupsToSpawnPerSide;
                    float maxAngle;
                    switch (qualityTier)
                    {
                        case QualityTier.Uncommon:
                            numPickupsToSpawnPerSide = 1;
                            maxAngle = 35f;
                            break;
                        case QualityTier.Rare:
                            numPickupsToSpawnPerSide = 2;
                            maxAngle = 90f;
                            break;
                        case QualityTier.Epic:
                            numPickupsToSpawnPerSide = 4;
                            maxAngle = 120f;
                            break;
                        case QualityTier.Legendary:
                            numPickupsToSpawnPerSide = 8;
                            maxAngle = 180f;
                            break;
                        default:
                            Log.Warning($"Quality tier {qualityTier} is not implemented");
                            numPickupsToSpawnPerSide = 0;
                            maxAngle = 0f;
                            break;
                    }

                    Vector3 bodyForward = self.characterBody.inputBank.aimDirection;
                    bodyForward.y = 0f;
                    bodyForward.Normalize();

                    GatewayQualityAttachment gatewayAttachment = GatewayQualityAttachment.FindAttachmentForBody(self.characterBody);

                    for (int i = 0; i < numPickupsToSpawnPerSide; i++)
                    {
                        float angleStep = maxAngle / numPickupsToSpawnPerSide;
                        float angle = angleStep * (i + 1);

                        const float ApproximatePickupDistance = 60f;

                        // Right
                        attemptSpawnPickup(angle);

                        // hack to prevent spawning 2 pickups in the same position directly behind the body
                        if (angle < 180f)
                        {
                            // Left
                            attemptSpawnPickup(-angle);
                        }

                        // Yes this can go over the limit, no I don't care
                        if (gatewayAttachment && gatewayAttachment.PickupLimitReached)
                            break;

                        void attemptSpawnPickup(float angle)
                        {
                            Vector3 approximatePosition = self.characterBody.corePosition + (Quaternion.AngleAxis(angle, Vector3.up) * bodyForward * ApproximatePickupDistance);
                            if (SceneInfo.instance.groundNodes)
                            {
                                NodeGraph.NodeIndex spawnNodeIndex = SceneInfo.instance.groundNodes.FindClosestNode(approximatePosition, self.characterBody.hullClassification, ApproximatePickupDistance / 3f);
                                if (SceneInfo.instance.groundNodes.GetNodePosition(spawnNodeIndex, out Vector3 nodePosition))
                                {
                                    GameObject pickupObject = GatewayQualityAttachment.SpawnPickup(nodePosition, self.characterBody);

                                    if (gatewayAttachment)
                                    {
                                        gatewayAttachment.RegisterPickupServer(pickupObject);
                                    }

                                    return;
                                }
                            }

                            Log.Debug("Failed to find pickup position");
                        }
                    }
                }
            }

            return success;
        }
    }
}
