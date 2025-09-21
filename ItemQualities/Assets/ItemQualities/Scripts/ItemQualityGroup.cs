using ItemQualities.ContentManagement;
using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using RoR2;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.Serialization;

using Path = System.IO.Path;

namespace ItemQualities
{
    [CreateAssetMenu(menuName = "ItemQualities/Items/ItemQualityGroup")]
    public class ItemQualityGroup : ScriptableObject, IAsyncContentLoadCallback
    {
        [HideInInspector]
        [NonSerialized]
        public ItemQualityGroupIndex GroupIndex = ItemQualityGroupIndex.Invalid;

        [FormerlySerializedAs("BaseItem")]
        public AssetReferenceT<ItemDef> BaseItemReference = new AssetReferenceT<ItemDef>(string.Empty);

        [FormerlySerializedAs("UncommonVariant")]
        [SerializeField]
        ItemDef _uncommonItem;

        [FormerlySerializedAs("RareVariant")]
        [SerializeField]
        ItemDef _rareItem;

        [FormerlySerializedAs("EpicVariant")]
        [SerializeField]
        ItemDef _epicItem;

        [FormerlySerializedAs("LegendaryVariant")]
        [SerializeField]
        ItemDef _legendaryItem;

        [HideInInspector]
        [NonSerialized]
        public ItemIndex BaseItemIndex = ItemIndex.None;

        public ItemIndex UncommonItemIndex => _uncommonItem ? _uncommonItem.itemIndex : ItemIndex.None;

        public ItemIndex RareItemIndex => _rareItem ? _rareItem.itemIndex : ItemIndex.None;

        public ItemIndex EpicItemIndex => _epicItem ? _epicItem.itemIndex : ItemIndex.None;

        public ItemIndex LegendaryItemIndex => _legendaryItem ? _legendaryItem.itemIndex : ItemIndex.None;

        public ItemIndex GetItemIndex(QualityTier qualityTier)
        {
            switch (qualityTier)
            {
                case QualityTier.None:
                    return BaseItemIndex;
                case QualityTier.Uncommon:
                    return UncommonItemIndex;
                case QualityTier.Rare:
                    return RareItemIndex;
                case QualityTier.Epic:
                    return EpicItemIndex;
                case QualityTier.Legendary:
                    return LegendaryItemIndex;
                default:
                    throw new NotImplementedException($"Quality tier '{qualityTier}' is not implemented");
            }
        }

        IEnumerator IAsyncContentLoadCallback.OnContentLoad(IProgress<float> progressReceiver)
        {
            AsyncOperationHandle<ItemDef> baseItemLoad = AddressableUtil.LoadTempAssetAsync(BaseItemReference);
            yield return baseItemLoad.AsProgressCoroutine(progressReceiver);

            if (baseItemLoad.Status != AsyncOperationStatus.Succeeded)
            {
                Log.Error($"Failed to load base item for quality group '{name}': {baseItemLoad.OperationException}");
                yield break;
            }

            ItemDef baseItem = baseItemLoad.Result;

            void populateItemAsset(ItemDef item, QualityTier qualityTier)
            {
                if (!item)
                {
                    Log.Warning($"Missing variant '{qualityTier}' in item group '{name}'");
                    return;
                }

                if (string.IsNullOrEmpty(item.nameToken))
                    item.nameToken = $"ITEM_{baseItem.name.ToUpper()}_{qualityTier.ToString().ToUpper()}_NAME";

                if (string.IsNullOrEmpty(item.pickupToken))
                    item.pickupToken = baseItem.pickupToken;

                if (string.IsNullOrEmpty(item.descriptionToken))
                    item.descriptionToken = baseItem.descriptionToken;

                if (string.IsNullOrEmpty(item.loreToken))
                    item.loreToken = baseItem.loreToken;

                if (!item.unlockableDef)
                    item.unlockableDef = baseItem.unlockableDef;

#pragma warning disable CS0618 // Type or member is obsolete
                if (!item.pickupModelPrefab)
                    item.pickupModelPrefab = baseItem.pickupModelPrefab;
#pragma warning restore CS0618 // Type or member is obsolete

                if (item.pickupModelReference == null || !item.pickupModelReference.RuntimeKeyIsValid())
                    item.pickupModelReference = baseItem.pickupModelReference;

                if (!item.pickupIconSprite)
                    item.pickupIconSprite = baseItem.pickupIconSprite;

                item.isConsumed = baseItem.isConsumed;
                item.hidden = baseItem.hidden;
                item.canRemove = baseItem.canRemove;

                HashSet<ItemTag> tags = new HashSet<ItemTag>(item.tags);
                tags.UnionWith(baseItem.tags);
                tags.Add(ItemTag.WorldUnique);
                item.tags = tags.ToArray();

                item.requiredExpansion = baseItem.requiredExpansion;
            }

            populateItemAsset(_uncommonItem, QualityTier.Uncommon);
            populateItemAsset(_rareItem, QualityTier.Rare);
            populateItemAsset(_epicItem, QualityTier.Epic);
            populateItemAsset(_legendaryItem, QualityTier.Legendary);
        }

#if UNITY_EDITOR
        [ContextMenu("Generate ItemDefs")]
        void GenerateItems()
        {
            string baseItemName = name;
            if (baseItemName.StartsWith("ig"))
                baseItemName = baseItemName.Substring(2);

            string currentDirectory = Path.GetDirectoryName(AssetDatabase.GetAssetPath(this));

            /*
            Texture2D iconTexture = BaseItemReference.LoadAssetAsync().WaitForCompletion().pickupIconSprite.texture;
            bool isTemporaryTexture = false;
            if (!iconTexture.isReadable)
            {
                RenderTexture tmp = RenderTexture.GetTemporary(iconTexture.width, iconTexture.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);

                Graphics.Blit(iconTexture, tmp);

                RenderTexture prevActive = RenderTexture.active;
                RenderTexture.active = tmp;

                iconTexture = new Texture2D(iconTexture.width, iconTexture.height);
                iconTexture.ReadPixels(new Rect(0, 0, tmp.width, tmp.height), 0, 0);
                iconTexture.Apply(false);

                RenderTexture.active = prevActive;

                RenderTexture.ReleaseTemporary(tmp);

                isTemporaryTexture = true;
            }

            File.WriteAllBytes(Path.Combine(currentDirectory, "test.png"), iconTexture.EncodeToPNG());
            AssetDatabase.ImportAsset(Path.Combine(currentDirectory, "test.png"));

            if (isTemporaryTexture)
            {
                Destroy(iconTexture);
            }
            */

            ItemTier itemTier = ItemTier.Tier1;
            if (currentDirectory.Contains("Tier1/"))
            {
                itemTier = ItemTier.Tier1;
            }
            else if (currentDirectory.Contains("Tier2/"))
            {
                itemTier = ItemTier.Tier2;
            }
            else if (currentDirectory.Contains("Tier3/"))
            {
                itemTier = ItemTier.Tier3;
            }
            else if (currentDirectory.Contains("Boss/"))
            {
                itemTier = ItemTier.Boss;
            }
            else if (currentDirectory.Contains("VoidBoss/"))
            {
                itemTier = ItemTier.VoidBoss;
            }
            else if (currentDirectory.Contains("VoidTier1/"))
            {
                itemTier = ItemTier.VoidTier1;
            }
            else if (currentDirectory.Contains("VoidTier2/"))
            {
                itemTier = ItemTier.VoidTier2;
            }
            else if (currentDirectory.Contains("VoidTier3/"))
            {
                itemTier = ItemTier.VoidTier3;
            }
            else if (currentDirectory.Contains("Lunar/"))
            {
                itemTier = ItemTier.Lunar;
            }
            else if (currentDirectory.Contains("NoTier/"))
            {
                itemTier = ItemTier.NoTier;
            }

            ItemDef createItem(QualityTier qualityTier)
            {
                ItemDef itemDef = ScriptableObject.CreateInstance<ItemDef>();
                itemDef.name = baseItemName + qualityTier;
                itemDef.descriptionToken = $"ITEM_{baseItemName.ToUpper()}_{qualityTier.ToString().ToUpper()}_DESC";
                itemDef.pickupToken = $"ITEM_{baseItemName.ToUpper()}_{qualityTier.ToString().ToUpper()}_PICKUP";
                itemDef.pickupIconSprite = AssetDatabase.LoadAssetAtPath<Sprite>(Path.Combine(currentDirectory, "tex" + itemDef.name + ".png"));
                itemDef.tags = new ItemTag[] { ItemTag.WorldUnique };

#pragma warning disable CS0618 // Type or member is obsolete
                itemDef.deprecatedTier = itemTier;
#pragma warning restore CS0618 // Type or member is obsolete

                AssetDatabase.CreateAsset(itemDef, Path.Combine(currentDirectory, itemDef.name + ".asset"));

                return itemDef;
            }

            if (!_uncommonItem)
            {
                _uncommonItem = createItem(QualityTier.Uncommon);
            }

            if (!_rareItem)
            {
                _rareItem = createItem(QualityTier.Rare);
            }

            if (!_epicItem)
            {
                _epicItem = createItem(QualityTier.Epic);
            }

            if (!_legendaryItem)
            {
                _legendaryItem = createItem(QualityTier.Legendary);
            }
        }
#endif
    }
}
