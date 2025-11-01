using ItemQualities.ContentManagement;
using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using RoR2;
using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

using Path = System.IO.Path;

namespace ItemQualities
{
    [CreateAssetMenu(menuName = "ItemQualities/Items/EquipmentQualityGroup")]
    public class EquipmentQualityGroup : ScriptableObject, IAsyncContentLoadCallback
    {
        [HideInInspector]
        [NonSerialized]
        public EquipmentQualityGroupIndex GroupIndex = EquipmentQualityGroupIndex.Invalid;

        public AssetReferenceT<EquipmentDef> BaseEquipmentReference = new AssetReferenceT<EquipmentDef>(string.Empty);

        [SerializeField]
        EquipmentDef _uncommonEquipment;

        [SerializeField]
        EquipmentDef _rareEquipment;

        [SerializeField]
        EquipmentDef _epicEquipment;

        [SerializeField]
        EquipmentDef _legendaryEquipment;

        [HideInInspector]
        [NonSerialized]
        public EquipmentIndex BaseEquipmentIndex = EquipmentIndex.None;

        public EquipmentIndex UncommonEquipmentIndex => _uncommonEquipment ? _uncommonEquipment.equipmentIndex : EquipmentIndex.None;

        public EquipmentIndex RareEquipmentIndex => _rareEquipment ? _rareEquipment.equipmentIndex : EquipmentIndex.None;

        public EquipmentIndex EpicEquipmentIndex => _epicEquipment ? _epicEquipment.equipmentIndex : EquipmentIndex.None;

        public EquipmentIndex LegendaryEquipmentIndex => _legendaryEquipment ? _legendaryEquipment.equipmentIndex : EquipmentIndex.None;

        public EquipmentIndex GetEquipmentIndex(QualityTier qualityTier)
        {
            switch (qualityTier)
            {
                case QualityTier.None:
                    return BaseEquipmentIndex;
                case QualityTier.Uncommon:
                    return UncommonEquipmentIndex;
                case QualityTier.Rare:
                    return RareEquipmentIndex;
                case QualityTier.Epic:
                    return EpicEquipmentIndex;
                case QualityTier.Legendary:
                    return LegendaryEquipmentIndex;
                default:
                    throw new NotImplementedException($"Quality tier '{qualityTier}' is not implemented");
            }
        }

        void OnValidate()
        {
            if (BaseEquipmentReference == null || !BaseEquipmentReference.RuntimeKeyIsValid())
            {
                Debug.LogError($"Invalid equipment address in group '{name}'");
            }
        }

        IEnumerator IAsyncContentLoadCallback.OnContentLoad(IProgress<float> progressReceiver)
        {
            if (BaseEquipmentReference == null || !BaseEquipmentReference.RuntimeKeyIsValid())
            {
                Log.Error($"Invalid equipment address in group '{name}'");
                progressReceiver.Report(1f);
                yield break;
            }

            AsyncOperationHandle<EquipmentDef> baseEquipmentLoad = AddressableUtil.LoadTempAssetAsync(BaseEquipmentReference);
            yield return baseEquipmentLoad.AsProgressCoroutine(progressReceiver);

            if (baseEquipmentLoad.Status != AsyncOperationStatus.Succeeded)
            {
                Log.Error($"Failed to load base equipment for quality group '{name}': {baseEquipmentLoad.OperationException}");
                yield break;
            }

            EquipmentDef baseEquipment = baseEquipmentLoad.Result;

            void populateEquipmentAsset(EquipmentDef equipment, QualityTier qualityTier)
            {
                if (!equipment)
                {
                    Log.Warning($"Missing variant '{qualityTier}' in item group '{name}'");
                    return;
                }

                if (string.IsNullOrEmpty(equipment.nameToken))
                    equipment.nameToken = $"ITEM_{baseEquipment.name.ToUpper()}_{qualityTier.ToString().ToUpper()}_NAME";

                if (string.IsNullOrEmpty(equipment.pickupToken))
                    equipment.pickupToken = baseEquipment.pickupToken;

                if (string.IsNullOrEmpty(equipment.descriptionToken))
                    equipment.descriptionToken = baseEquipment.descriptionToken;

                if (string.IsNullOrEmpty(equipment.loreToken))
                    equipment.loreToken = baseEquipment.loreToken;

                if (!equipment.unlockableDef)
                    equipment.unlockableDef = baseEquipment.unlockableDef;

#pragma warning disable CS0618 // Type or member is obsolete
                if (!equipment.pickupModelPrefab)
                    equipment.pickupModelPrefab = baseEquipment.pickupModelPrefab;
#pragma warning restore CS0618 // Type or member is obsolete

                if (equipment.pickupModelReference == null || !equipment.pickupModelReference.RuntimeKeyIsValid())
                    equipment.pickupModelReference = baseEquipment.pickupModelReference;

                if (!equipment.pickupIconSprite)
                    equipment.pickupIconSprite = baseEquipment.pickupIconSprite;

                if (equipment.colorIndex == ColorCatalog.ColorIndex.None)
                    equipment.colorIndex = baseEquipment.colorIndex;

                if (!equipment.passiveBuffDef)
                    equipment.passiveBuffDef = baseEquipment.passiveBuffDef;

                equipment.cooldown *= baseEquipment.cooldown;

                equipment.isConsumed = baseEquipment.isConsumed;
                equipment.isLunar = baseEquipment.isLunar;
                equipment.isBoss = baseEquipment.isBoss;

                equipment.canBeRandomlyTriggered = baseEquipment.canBeRandomlyTriggered;
                equipment.enigmaCompatible = baseEquipment.enigmaCompatible;

                equipment.appearsInSinglePlayer = baseEquipment.appearsInSinglePlayer;
                equipment.appearsInMultiPlayer = baseEquipment.appearsInMultiPlayer;

                equipment.requiredExpansion = baseEquipment.requiredExpansion;
            }

            populateEquipmentAsset(_uncommonEquipment, QualityTier.Uncommon);
            populateEquipmentAsset(_rareEquipment, QualityTier.Rare);
            populateEquipmentAsset(_epicEquipment, QualityTier.Epic);
            populateEquipmentAsset(_legendaryEquipment, QualityTier.Legendary);
        }

#if UNITY_EDITOR
        [ContextMenu("Generate EquipmentDefs")]
        void GenerateEquipments()
        {
            string baseEquipmentName = name;
            if (baseEquipmentName.StartsWith("eg"))
                baseEquipmentName = baseEquipmentName.Substring(2);

            string currentDirectory = Path.GetDirectoryName(AssetDatabase.GetAssetPath(this));

            AsyncOperationHandle<EquipmentDef> baseEquipmentLoadHandle = BaseEquipmentReference.LoadAssetAsync<EquipmentDef>();
            using ScopedAsyncOperationHandle<EquipmentDef> baseEquipmentLoadScope = new ScopedAsyncOperationHandle<EquipmentDef>(baseEquipmentLoadHandle);

            EquipmentDef baseEquipmentDef = baseEquipmentLoadHandle.WaitForCompletion();

            Texture2D baseIconTexture = baseEquipmentDef.pickupIconSprite.texture;

            EquipmentDef createEquipment(QualityTier qualityTier)
            {
                EquipmentDef equipmentDef = ScriptableObject.CreateInstance<EquipmentDef>();
                equipmentDef.name = baseEquipmentName + qualityTier;
                equipmentDef.descriptionToken = $"EQUIPMENT_{baseEquipmentName.ToUpper()}_{qualityTier.ToString().ToUpper()}_DESC";
                equipmentDef.pickupToken = $"EQUIPMENT_{baseEquipmentName.ToUpper()}_{qualityTier.ToString().ToUpper()}_PICKUP";
                equipmentDef.cooldown = 1f;
                equipmentDef.colorIndex = ColorCatalog.ColorIndex.None;
                equipmentDef.canDrop = false;
                equipmentDef.dropOnDeathChance = 0f;

                string qualityIconTextureAssetPath = Path.Combine(currentDirectory, "tex" + equipmentDef.name + ".png");
                equipmentDef.pickupIconSprite = AssetDatabase.LoadAssetAtPath<Sprite>(qualityIconTextureAssetPath);
                if (!equipmentDef.pickupIconSprite)
                {
                    Texture2D qualityIconTexture = QualityCatalog.CreateQualityIconTexture(baseIconTexture, qualityTier);

                    File.WriteAllBytes(qualityIconTextureAssetPath, qualityIconTexture.EncodeToPNG());

                    AssetDatabase.ImportAsset(qualityIconTextureAssetPath);

                    TextureImporter textureImporter = (TextureImporter)AssetImporter.GetAtPath(qualityIconTextureAssetPath);
                    textureImporter.textureType = TextureImporterType.Sprite;
                    textureImporter.spritePixelsPerUnit = qualityIconTexture.width / 5.12f;
                    textureImporter.alphaIsTransparency = true;

                    AssetDatabase.ImportAsset(qualityIconTextureAssetPath, ImportAssetOptions.ForceUpdate);

                    equipmentDef.pickupIconSprite = AssetDatabase.LoadAssetAtPath<Sprite>(qualityIconTextureAssetPath);
                }

                AssetDatabase.CreateAsset(equipmentDef, Path.Combine(currentDirectory, equipmentDef.name + ".asset"));

                return equipmentDef;
            }

            if (!_uncommonEquipment)
            {
                _uncommonEquipment = createEquipment(QualityTier.Uncommon);
            }

            if (!_rareEquipment)
            {
                _rareEquipment = createEquipment(QualityTier.Rare);
            }

            if (!_epicEquipment)
            {
                _epicEquipment = createEquipment(QualityTier.Epic);
            }

            if (!_legendaryEquipment)
            {
                _legendaryEquipment = createEquipment(QualityTier.Legendary);
            }
        }
#endif
    }
}
