using ItemQualities.ContentManagement;
using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using R2API;
using RoR2;
using RoR2BepInExPack.GameAssetPathsBetter;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace ItemQualities.Items
{
    internal static class SprintArmor
    {
        public static GameObject BucklerDefenseBigPrefab;

        [ContentInitializer]
        private static IEnumerator LoadContent(ContentInitializerArgs args)
        {
            AsyncOperationHandle<GameObject> bucklerDefenseLoad = AddressableUtil.LoadTempAssetAsync<GameObject>(RoR2_Base_SprintArmor.BucklerDefense_prefab);
            bucklerDefenseLoad.OnSuccess(bucklerDefense =>
            {
                BucklerDefenseBigPrefab = bucklerDefense.InstantiateClone("BucklerDefenseBig", false);

                Transform meshHolder = BucklerDefenseBigPrefab.transform.Find("MeshHolder");
                Dictionary<Material, Material> materialCache = new Dictionary<Material, Material>();
                for (int i = 0; i < meshHolder.childCount; i++)
                {
                    Transform child = meshHolder.GetChild(i);
                    if (child && child.TryGetComponent(out MeshRenderer meshRenderer))
                    {
                        Material material = meshRenderer.sharedMaterial;
                        if (material)
                        {
                            if (!materialCache.TryGetValue(material, out Material redMaterial))
                            {
                                redMaterial = new Material(material);
                                redMaterial.name = $"{material.name}_Red";

                                redMaterial.SetColor(ShaderProperties._TintColor, new Color(0.9f, 0f, 0f));
                                redMaterial.SetColor(ShaderProperties._Color, new Color(0.9f, 0f, 0f));
                                materialCache.Add(material, redMaterial);
                            }

                            meshRenderer.sharedMaterial = redMaterial;
                        }
                    }
                }

                RotateObject rotateObject = BucklerDefenseBigPrefab.GetComponentInChildren<RotateObject>(true);
                if (rotateObject)
                {
                    rotateObject.rotationSpeed *= 0.5f;
                }
            });

            return bucklerDefenseLoad.AsProgressCoroutine(args.ProgressReceiver);
        }

        [SystemInitializer]
        private static void Init()
        {
            RecalculateStatsAPI.GetStatCoefficients += getStatCoefficients;
        }

        private static void getStatCoefficients(CharacterBody sender, RecalculateStatsAPI.StatHookEventArgs args)
        {
            if (!sender.inventory)
                return;

            ItemQualityCounts sprintArmor = sender.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.SprintArmor);

            if (sender.HasBuff(ItemQualitiesContent.Buffs.SprintArmorWeaken))
            {
                args.armorAdd -= 20;
                args.damageTotalMult *= 0.6f;
                args.moveSpeedTotalMult *= 0.6f;
            }
        }
    }
}
