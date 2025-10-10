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

        public ItemQualityCounts GetItemCounts(Inventory inventory)
        {
            if (!inventory)
                return default;

            int baseItemCount = inventory.GetItemCount(BaseItemIndex);
            int uncommonItemCount = inventory.GetItemCount(UncommonItemIndex);
            int rareItemCount = inventory.GetItemCount(RareItemIndex);
            int epicItemCount = inventory.GetItemCount(EpicItemIndex);
            int legendaryItemCount = inventory.GetItemCount(LegendaryItemIndex);

            return new ItemQualityCounts(baseItemCount, uncommonItemCount, rareItemCount, epicItemCount, legendaryItemCount);
        }

        public QualityTier GetHighestQualityInInventory(Inventory inventory)
        {
            return GetHighestQualityInInventory(inventory, out _);
        }

        public QualityTier GetHighestQualityInInventory(Inventory inventory, out int itemCount)
        {
            if (inventory)
            {
                for (QualityTier qualityTier = QualityTier.Count - 1; qualityTier >= QualityTier.None; qualityTier--)
                {
                    itemCount = inventory.GetItemCount(GetItemIndex(qualityTier));
                    if (itemCount > 0)
                    {
                        return qualityTier;
                    }
                }
            }

            itemCount = 0;
            return QualityTier.None;
        }

        public ItemQualityCounts GetTeamItemCounts(TeamIndex teamIndex, bool requireAlive, bool requireConnected = true)
        {
            ItemQualityCounts itemCounts = default;

            foreach (CharacterMaster master in CharacterMaster.readOnlyInstancesList)
            {
                if (!master)
                    continue;

                if (master.teamIndex != teamIndex)
                    continue;

                CharacterBody body = master.GetBody();
                if (requireAlive && (!body || !body.healthComponent || !body.healthComponent.alive))
                    continue;

                if (requireConnected && (!master.playerCharacterMasterController || !master.playerCharacterMasterController.isConnected))
                    continue;

                itemCounts += GetItemCounts(master.inventory);
            }

            return itemCounts;
        }

        void OnValidate()
        {
            if (BaseItemReference == null || !BaseItemReference.RuntimeKeyIsValid())
            {
                Debug.LogError($"Invalid item address in group '{name}'");
            }
        }

        IEnumerator IAsyncContentLoadCallback.OnContentLoad(IProgress<float> progressReceiver)
        {
            if (BaseItemReference == null || !BaseItemReference.RuntimeKeyIsValid())
            {
                Log.Error($"Invalid item address in group '{name}'");
                progressReceiver.Report(1f);
                yield break;
            }

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

#pragma warning disable CS0618 // Type or member is obsolete
                item.deprecatedTier = baseItem.deprecatedTier;
#pragma warning restore CS0618 // Type or member is obsolete
                item._itemTierDef = baseItem._itemTierDef;

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

            ItemDef baseItemDef = BaseItemReference.OperationHandle.IsValid()
                ? (ItemDef)BaseItemReference.OperationHandle.Result
                : BaseItemReference.LoadAssetAsync<ItemDef>().WaitForCompletion();

            Texture2D iconTexture = baseItemDef.pickupIconSprite.texture;


            Sprite[] qualitySprites = AssetDatabase.LoadAllAssetsAtPath("Assets/ItemQualities/Assets/texQualityIcons.png")
                .OfType<Sprite>()
                .OrderBy(x => x.name)
                .Skip(1)
                .Take(4)
                .ToArray();

            ItemDef createItem(QualityTier qualityTier)
            {
                ItemDef itemDef = ScriptableObject.CreateInstance<ItemDef>();
                itemDef.name = baseItemName + qualityTier;
                itemDef.descriptionToken = $"ITEM_{baseItemName.ToUpper()}_{qualityTier.ToString().ToUpper()}_DESC";
                itemDef.pickupToken = $"ITEM_{baseItemName.ToUpper()}_{qualityTier.ToString().ToUpper()}_PICKUP";
                itemDef.pickupIconSprite = AssetDatabase.LoadAssetAtPath<Sprite>(Path.Combine(currentDirectory, "tex" + itemDef.name + ".png"));
                if (!itemDef.pickupIconSprite)
                {
                    itemDef.pickupIconSprite = CreateItemSprite(iconTexture, qualitySprites[(int)qualityTier]);
                    //todo: make it save as a png file
                    AssetDatabase.CreateAsset(itemDef.pickupIconSprite, Path.Combine(currentDirectory, "tex" + itemDef.name + ".asset"));
                }
                itemDef.tags = new ItemTag[] { ItemTag.WorldUnique };

#pragma warning disable CS0618 // Type or member is obsolete
                itemDef.deprecatedTier = baseItemDef.tier;
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

        private static Sprite CreateItemSprite(Texture2D baseItemTex, Sprite qualityIconSprite)
        {
            //https://forum.unity.com/threads/easy-way-to-make-texture-isreadable-true-by-script.1141915/
            RenderTexture renderTex = RenderTexture.GetTemporary(
                baseItemTex.width,
                baseItemTex.height,
                0,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Linear);

            Graphics.Blit(baseItemTex, renderTex);

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = renderTex;

            Texture2D itemTexture = new Texture2D(renderTex.width, renderTex.height);
            itemTexture.ReadPixels(new Rect(0, 0, renderTex.width, renderTex.height), 0, 0);

            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(renderTex);

            int width = itemTexture.width / 2;
            int height = itemTexture.height / 2;
            float uvLeft = qualityIconSprite.rect.x / (qualityIconSprite.texture.width);
            float uvRight = (qualityIconSprite.rect.x + qualityIconSprite.rect.width) / (qualityIconSprite.texture.width);
            float uvBottom = qualityIconSprite.rect.y / (qualityIconSprite.texture.height);
            float uvTop = (qualityIconSprite.rect.y + qualityIconSprite.rect.height) / (qualityIconSprite.texture.height);

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    Color c = qualityIconSprite.texture.GetPixelBilinear(Mathf.Lerp(uvLeft, uvRight, (float)x / width), Mathf.Lerp(uvBottom, uvTop, (float)y / height));
                    if (c.a > 0)
                    {
                        itemTexture.SetPixel(x, y + height, c);
                    }
                }
            }

            itemTexture.Apply();
            
            Sprite sprite = Sprite.Create(
                itemTexture,
                new Rect(0, 0, itemTexture.width, itemTexture.height),
                new Vector2(0.5f, 0.5f)
            );

            return sprite;
        }
#endif
    }
}
