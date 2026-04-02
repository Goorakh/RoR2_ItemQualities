using ItemQualities.Utilities.Extensions;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace ItemQualities.Equipments
{
    static class Gateway
    {
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
