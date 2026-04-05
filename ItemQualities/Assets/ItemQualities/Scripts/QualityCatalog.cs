using HG;
using HG.Coroutines;
using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using R2API;
using RoR2;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace ItemQualities
{
    public static class QualityCatalog
    {
        static readonly QualityTierDef[] _qualityTierDefs = new QualityTierDef[(int)QualityTier.Count];

        static ItemQualityGroup[] _allItemQualityGroups = Array.Empty<ItemQualityGroup>();
        static QualityTier[] _itemIndexToQuality = Array.Empty<QualityTier>();
        static ItemQualityGroupIndex[] _itemIndexToQualityGroupIndex = Array.Empty<ItemQualityGroupIndex>();
        static readonly ReadOnlyArray<ItemIndex>[] _itemsByQualityTier = new ReadOnlyArray<ItemIndex>[(int)QualityTier.Count + 1];

        static EquipmentQualityGroup[] _allEquipmentQualityGroups = Array.Empty<EquipmentQualityGroup>();
        static QualityTier[] _equipmentIndexToQuality = Array.Empty<QualityTier>();
        static EquipmentQualityGroupIndex[] _equipmentIndexToQualityGroupIndex = Array.Empty<EquipmentQualityGroupIndex>();
        static readonly ReadOnlyArray<EquipmentIndex>[] _equipmentsByQualityTier = new ReadOnlyArray<EquipmentIndex>[(int)QualityTier.Count + 1];

        static BuffQualityGroup[] _allBuffQualityGroups = Array.Empty<BuffQualityGroup>();
        static QualityTier[] _buffIndexToQuality = Array.Empty<QualityTier>();
        static BuffQualityGroupIndex[] _buffIndexToQualityGroupIndex = Array.Empty<BuffQualityGroupIndex>();
        static readonly ReadOnlyArray<BuffIndex>[] _buffsByQualityTier = new ReadOnlyArray<BuffIndex>[(int)QualityTier.Count + 1];

        public static int ItemQualityGroupCount => _allItemQualityGroups.Length;

        public static int EquipmentQualityGroupCount => _allEquipmentQualityGroups.Length;

        public static int BuffQualityGroupCount => _allBuffQualityGroups.Length;

        public static ResourceAvailability Availability = new ResourceAvailability();

        [SystemInitializer(typeof(ItemCatalog), typeof(EquipmentCatalog), typeof(BuffCatalog))]
        static IEnumerator Init()
        {
            yield return SetQualityGroups(ItemQualitiesContent.QualityTiers.AllQualityTiers,
                                          ItemQualitiesContent.ItemQualityGroups.AllGroups,
                                          ItemQualitiesContent.EquipmentQualityGroups.AllGroups,
                                          ItemQualitiesContent.BuffQualityGroups.AllGroups);

            Availability.MakeAvailable();
        }

        static IEnumerator SetQualityGroups(IReadOnlyCollection<QualityTierDef> qualityTierDefs,
                                            IReadOnlyCollection<ItemQualityGroup> itemQualityGroups,
                                            IReadOnlyCollection<EquipmentQualityGroup> equipmentQualityGroups,
                                            IReadOnlyCollection<BuffQualityGroup> buffQualityGroups)
        {
            foreach (QualityTierDef qualityTierDef in qualityTierDefs)
            {
                _qualityTierDefs[(int)qualityTierDef.qualityTier] = qualityTierDef;
            }

            foreach (ItemQualityGroup itemQualityGroup in _allItemQualityGroups)
            {
                itemQualityGroup.GroupIndex = ItemQualityGroupIndex.Invalid;
            }

            static void sortUnityObjectsByName(UnityEngine.Object[] array, StringComparison stringComparison = StringComparison.Ordinal)
            {
                string[] keys = new string[array.Length];
                for (int i = 0; i < array.Length; i++)
                {
                    keys[i] = array[i].name;
                }

                Array.Sort(keys, array, StringComparer.FromComparison(stringComparison));
            }

            _allItemQualityGroups = itemQualityGroups.ToArray();
            sortUnityObjectsByName(_allItemQualityGroups);

            Array.Resize(ref _itemIndexToQuality, ItemCatalog.itemCount);
            Array.Fill(_itemIndexToQuality, QualityTier.None);

            Array.Resize(ref _itemIndexToQualityGroupIndex, ItemCatalog.itemCount);
            Array.Fill(_itemIndexToQualityGroupIndex, ItemQualityGroupIndex.Invalid);

            foreach (EquipmentQualityGroup equipmentQualityGroup in _allEquipmentQualityGroups)
            {
                equipmentQualityGroup.GroupIndex = EquipmentQualityGroupIndex.Invalid;
            }

            _allEquipmentQualityGroups = equipmentQualityGroups.ToArray();
            sortUnityObjectsByName(_allEquipmentQualityGroups);

            Array.Resize(ref _equipmentIndexToQuality, EquipmentCatalog.equipmentCount);
            Array.Fill(_equipmentIndexToQuality, QualityTier.None);

            Array.Resize(ref _equipmentIndexToQualityGroupIndex, EquipmentCatalog.equipmentCount);
            Array.Fill(_equipmentIndexToQualityGroupIndex, EquipmentQualityGroupIndex.Invalid);

            foreach (BuffQualityGroup buffQualityGroup in _allBuffQualityGroups)
            {
                buffQualityGroup.GroupIndex = BuffQualityGroupIndex.Invalid;
            }

            _allBuffQualityGroups = buffQualityGroups.ToArray();
            sortUnityObjectsByName(_allBuffQualityGroups);

            Array.Resize(ref _buffIndexToQuality, BuffCatalog.buffCount);
            Array.Fill(_buffIndexToQuality, QualityTier.None);

            Array.Resize(ref _buffIndexToQualityGroupIndex, BuffCatalog.buffCount);
            Array.Fill(_buffIndexToQualityGroupIndex, BuffQualityGroupIndex.Invalid);

            ParallelCoroutine baseAssetsParallelLoadCoroutine = new ParallelCoroutine();

            for (int i = 0; i < _allItemQualityGroups.Length; i++)
            {
                ItemQualityGroupIndex itemQualityGroupIndex = (ItemQualityGroupIndex)i;
                ItemQualityGroup itemQualityGroup = _allItemQualityGroups[i];
                itemQualityGroup.GroupIndex = itemQualityGroupIndex;

                void recordItemInGroup(ItemIndex itemIndex, QualityTier qualityTier)
                {
                    if (itemIndex == ItemIndex.None)
                        return;
                    
                    if (_itemIndexToQualityGroupIndex[(int)itemIndex] != ItemQualityGroupIndex.Invalid)
                    {
                        Log.Error($"Item {ItemCatalog.GetItemDef(itemIndex)} is registered in several quality groups, ({GetItemQualityGroup(_itemIndexToQualityGroupIndex[(int)itemIndex])} & {GetItemQualityGroup(itemQualityGroupIndex)})");
                        return;
                    }

                    _itemIndexToQuality[(int)itemIndex] = qualityTier;
                    _itemIndexToQualityGroupIndex[(int)itemIndex] = itemQualityGroupIndex;
                }

                for (QualityTier qualityTier = 0; qualityTier < QualityTier.Count; qualityTier++)
                {
                    ItemIndex qualityItemIndex = itemQualityGroup.GetItemIndex(qualityTier);
                    if (qualityItemIndex == ItemIndex.None)
                    {
                        ItemDef qualityItemDef = itemQualityGroup.GetItemDef(qualityTier);
                        if (qualityItemDef)
                        {
                            Log.Error($"Item '{qualityItemDef.name}' ({qualityTier} variant in group '{itemQualityGroup.name}') is not registered to the catalog.");
                        }
                        else
                        {
                            Log.Warning($"No item registered as {qualityTier} variant in group '{itemQualityGroup.name}'");
                        }
                    }
                    else
                    {
                        recordItemInGroup(qualityItemIndex, qualityTier);
                    }
                }

                void recordBaseItem(ItemDef baseItem)
                {
                    if (baseItem.itemIndex != ItemIndex.None)
                    {
                        itemQualityGroup.BaseItemIndex = baseItem.itemIndex;
                        recordItemInGroup(baseItem.itemIndex, QualityTier.None);
                    }
                    else
                    {
                        Log.Error($"Base item ({baseItem}) in group {itemQualityGroup} is not registered in the catalog.");
                    }
                }

                if (itemQualityGroup.BaseItem)
                {
                    recordBaseItem(itemQualityGroup.BaseItem);
                }
                else if (itemQualityGroup.BaseItemReference != null && itemQualityGroup.BaseItemReference.RuntimeKeyIsValid())
                {
                    AsyncOperationHandle<ItemDef> baseItemLoad = AddressableUtil.LoadTempAssetAsync(itemQualityGroup.BaseItemReference);
                    baseItemLoad.OnSuccess(recordBaseItem);

                    baseAssetsParallelLoadCoroutine.Add(baseItemLoad);
                }
            }

            for (int i = 0; i < _allEquipmentQualityGroups.Length; i++)
            {
                EquipmentQualityGroupIndex equipmentQualityGroupIndex = (EquipmentQualityGroupIndex)i;
                EquipmentQualityGroup equipmentQualityGroup = _allEquipmentQualityGroups[i];
                equipmentQualityGroup.GroupIndex = equipmentQualityGroupIndex;

                void recordEquipmentInGroup(EquipmentIndex equipmentIndex, QualityTier qualityTier)
                {
                    if (equipmentIndex == EquipmentIndex.None)
                        return;

                    if (_equipmentIndexToQualityGroupIndex[(int)equipmentIndex] != EquipmentQualityGroupIndex.Invalid)
                    {
                        Log.Error($"Equipment '{EquipmentCatalog.GetEquipmentDef(equipmentIndex).name}' is registered in several quality groups, ('{GetEquipmentQualityGroup(_equipmentIndexToQualityGroupIndex[(int)equipmentIndex]).name}' and '{GetEquipmentQualityGroup(equipmentQualityGroupIndex).name}')");
                        return;
                    }

                    _equipmentIndexToQuality[(int)equipmentIndex] = qualityTier;
                    _equipmentIndexToQualityGroupIndex[(int)equipmentIndex] = equipmentQualityGroupIndex;
                }

                for (QualityTier qualityTier = 0; qualityTier < QualityTier.Count; qualityTier++)
                {
                    EquipmentIndex qualityEquipmentIndex = equipmentQualityGroup.GetEquipmentIndex(qualityTier);
                    if (qualityEquipmentIndex == EquipmentIndex.None)
                    {
                        EquipmentDef qualityEquipmentDef = equipmentQualityGroup.GetEquipmentDef(qualityTier);
                        if (qualityEquipmentDef)
                        {
                            Log.Error($"Equipment {qualityEquipmentDef.name} ({qualityTier} variant in group '{equipmentQualityGroup.name}') is not registered to the catalog.");
                        }
                        else
                        {
                            Log.Warning($"No equipment registered as {qualityTier} variant in group '{equipmentQualityGroup.name}'");
                        }
                    }
                    else
                    {
                        recordEquipmentInGroup(qualityEquipmentIndex, qualityTier);
                    }
                }

                void recordBaseEquipment(EquipmentDef baseEquipment)
                {
                    if (baseEquipment.equipmentIndex != EquipmentIndex.None)
                    {
                        equipmentQualityGroup.BaseEquipmentIndex = baseEquipment.equipmentIndex;
                        recordEquipmentInGroup(baseEquipment.equipmentIndex, QualityTier.None);
                    }
                    else
                    {
                        Log.Error($"Base equipment ({baseEquipment.name}) in group '{equipmentQualityGroup.name}' is not registered in the catalog.");
                    }
                }

                if (equipmentQualityGroup.BaseEquipment)
                {
                    recordBaseEquipment(equipmentQualityGroup.BaseEquipment);
                }
                else if (equipmentQualityGroup.BaseEquipmentReference != null && equipmentQualityGroup.BaseEquipmentReference.RuntimeKeyIsValid())
                {
                    AsyncOperationHandle<EquipmentDef> baseEquipmentLoad = AddressableUtil.LoadTempAssetAsync(equipmentQualityGroup.BaseEquipmentReference);
                    baseEquipmentLoad.OnSuccess(recordBaseEquipment);

                    baseAssetsParallelLoadCoroutine.Add(baseEquipmentLoad);
                }
                else
                {
                    Log.Error($"No base equipment defined for quality group '{equipmentQualityGroup.name}'");
                }
            }

            for (int i = 0; i < _allBuffQualityGroups.Length; i++)
            {
                BuffQualityGroupIndex buffQualityGroupIndex = (BuffQualityGroupIndex)i;
                BuffQualityGroup buffQualityGroup = _allBuffQualityGroups[i];
                buffQualityGroup.GroupIndex = buffQualityGroupIndex;

                void recordBuffInGroup(BuffIndex buffIndex, QualityTier qualityTier)
                {
                    if (buffIndex == BuffIndex.None)
                        return;

                    if (_buffIndexToQualityGroupIndex[(int)buffIndex] != BuffQualityGroupIndex.Invalid)
                    {
                        Log.Error($"Buff {BuffCatalog.GetBuffDef(buffIndex)} is registered in several quality groups, ({GetBuffQualityGroup(_buffIndexToQualityGroupIndex[(int)buffIndex])} & {GetBuffQualityGroup(buffQualityGroupIndex)})");
                        return;
                    }

                    _buffIndexToQuality[(int)buffIndex] = qualityTier;
                    _buffIndexToQualityGroupIndex[(int)buffIndex] = buffQualityGroupIndex;
                }

                for (QualityTier qualityTier = 0; qualityTier < QualityTier.Count; qualityTier++)
                {
                    BuffIndex qualityBuffIndex = buffQualityGroup.GetBuffIndex(qualityTier);
                    if (qualityBuffIndex == BuffIndex.None)
                    {
                        BuffDef qualityBuffDef = buffQualityGroup.GetBuffDef(qualityTier);
                        if (qualityBuffDef)
                        {
                            Log.Error($"Buff {qualityBuffDef.name} ({qualityTier} variant in group '{buffQualityGroup.name}') is not registered to the catalog.");
                        }
                        else
                        {
                            Log.Warning($"No buff registered as {qualityTier} variant in group '{buffQualityGroup.name}'");
                        }
                    }
                    else
                    {
                        recordBuffInGroup(qualityBuffIndex, qualityTier);
                    }
                }

                void recordBaseBuff(BuffDef baseBuff)
                {
                    if (baseBuff.buffIndex != BuffIndex.None)
                    {
                        buffQualityGroup.BaseBuffIndex = baseBuff.buffIndex;
                        recordBuffInGroup(baseBuff.buffIndex, QualityTier.None);
                    }
                    else
                    {
                        Log.Error($"Base buff ({baseBuff.name}) in group '{buffQualityGroup.name}' is not registered in the catalog.");
                    }
                }

                if (buffQualityGroup.BaseBuff)
                {
                    recordBaseBuff(buffQualityGroup.BaseBuff);
                }
                else if (buffQualityGroup.BaseBuffReference != null && buffQualityGroup.BaseBuffReference.RuntimeKeyIsValid())
                {
                    AsyncOperationHandle<BuffDef> baseBuffLoad = AddressableUtil.LoadTempAssetAsync(buffQualityGroup.BaseBuffReference);
                    baseBuffLoad.OnSuccess(recordBaseBuff);

                    baseAssetsParallelLoadCoroutine.Add(baseBuffLoad);
                }
            }

            yield return baseAssetsParallelLoadCoroutine;

            List<ItemIndex>[] itemsByQuality = new List<ItemIndex>[(int)QualityTier.Count + 1];
            List<EquipmentIndex>[] equipmentsByQuality = new List<EquipmentIndex>[(int)QualityTier.Count + 1];
            List<BuffIndex>[] buffsByQuality = new List<BuffIndex>[(int)QualityTier.Count + 1];

            for (QualityTier qualityTier = QualityTier.None; qualityTier < QualityTier.Count; qualityTier++)
            {
                List<ItemIndex> items = ListPool<ItemIndex>.RentCollection();
                items.EnsureCapacity(ItemCatalog.itemCount / ((int)QualityTier.Count + 1));
                itemsByQuality[(int)qualityTier + 1] = items;

                List<EquipmentIndex> equipments = ListPool<EquipmentIndex>.RentCollection();
                items.EnsureCapacity(EquipmentCatalog.equipmentCount / ((int)QualityTier.Count + 1));
                equipmentsByQuality[(int)qualityTier + 1] = equipments;

                List<BuffIndex> buffs = ListPool<BuffIndex>.RentCollection();
                buffs.EnsureCapacity(BuffCatalog.buffCount / ((int)QualityTier.Count + 1));
                buffsByQuality[(int)qualityTier + 1] = buffs;
            }

            for (ItemIndex itemIndex = 0; (int)itemIndex < ItemCatalog.itemCount; itemIndex++)
            {
                itemsByQuality[(int)GetQualityTier(itemIndex) + 1].Add(itemIndex);
            }

            for (EquipmentIndex equipmentIndex = 0; (int)equipmentIndex < EquipmentCatalog.equipmentCount; equipmentIndex++)
            {
                equipmentsByQuality[(int)GetQualityTier(equipmentIndex) + 1].Add(equipmentIndex);
            }

            for (BuffIndex buffIndex = 0; (int)buffIndex < BuffCatalog.buffCount; buffIndex++)
            {
                buffsByQuality[(int)GetQualityTier(buffIndex) + 1].Add(buffIndex);
            }

            for (QualityTier qualityTier = QualityTier.None; qualityTier < QualityTier.Count; qualityTier++)
            {
                List<ItemIndex> items = itemsByQuality[(int)qualityTier + 1];
                _itemsByQualityTier[(int)qualityTier + 1] = items.Count > 0 ? items.ToArray() : Array.Empty<ItemIndex>();

                ListPool<ItemIndex>.ReturnCollection(items);

                List<EquipmentIndex> equipments = equipmentsByQuality[(int)qualityTier + 1];
                _equipmentsByQualityTier[(int)qualityTier + 1] = equipments.Count > 0 ? equipments.ToArray() : Array.Empty<EquipmentIndex>();

                ListPool<EquipmentIndex>.ReturnCollection(equipments);

                List<BuffIndex> buffs = buffsByQuality[(int)qualityTier + 1];
                _buffsByQualityTier[(int)qualityTier + 1] = buffs.Count > 0 ? buffs.ToArray() : Array.Empty<BuffIndex>();

                ListPool<BuffIndex>.ReturnCollection(buffs);
            }

            List<Language> tempLoadedLanguages = new List<Language>();
            ParallelCoroutine languagesLoad = new ParallelCoroutine();

            foreach (Language language in Language.GetAllLanguages())
            {
                if (!language.stringsLoaded)
                {
                    Language.LanguageLoaderCoroutine languageLoader = new Language.LanguageLoaderCoroutine(language);
                    languagesLoad.Add(languageLoader.LoadStringsWithYield());
                    tempLoadedLanguages.Add(language);
                }
            }

            if (tempLoadedLanguages.Count > 0)
            {
                Log.Debug($"Loading strings from {tempLoadedLanguages.Count} language(s)");
                yield return languagesLoad;
            }

            static void formatQualityNameTokens(string baseNameToken, string qualityNameToken, string qualityModifierToken, Dictionary<string, Dictionary<string, string>> qualityLanguageDictionary)
            {
                if (string.IsNullOrEmpty(qualityNameToken))
                    return;
                
                foreach (Language language in Language.GetAllLanguages())
                {
                    if (!language.TokenIsRegistered(qualityNameToken))
                    {
                        string generatedQualityName = language.GetLocalizedFormattedStringByToken(qualityModifierToken, language.GetLocalizedStringByToken(baseNameToken));
                        qualityLanguageDictionary[language.name][qualityNameToken] = generatedQualityName;
                    }
                }
            }

            static void formatQualityTokens(string baseToken, string qualityToken, Dictionary<string, Dictionary<string, string>> qualityLanguageDictionary)
            {
                if (string.IsNullOrEmpty(qualityToken))
                    return;

                foreach (Language language in Language.GetAllLanguages())
                {
                    string qualityString = language.GetLocalizedStringByToken(qualityToken);

                    if (qualityString.Contains("{0}"))
                    {
                        qualityString = string.Format(qualityString, language.GetLocalizedStringByToken(baseToken));

                        qualityLanguageDictionary[language.name][qualityToken] = qualityString;
                    }
                }
            }

            Dictionary<string, Dictionary<string, string>> qualityLanguageDictionary = new Dictionary<string, Dictionary<string, string>>();
            foreach (Language language in Language.GetAllLanguages())
            {
                qualityLanguageDictionary.Add(language.name, new Dictionary<string, string>());
            }

            foreach (ItemQualityGroup itemQualityGroup in _allItemQualityGroups)
            {
                ItemDef baseItem = ItemCatalog.GetItemDef(itemQualityGroup.BaseItemIndex);
                if (!baseItem)
                    continue;

                for (QualityTier qualityTier = 0; qualityTier < QualityTier.Count; qualityTier++)
                {
                    ItemIndex itemIndex = itemQualityGroup.GetItemIndex(qualityTier);
                    ItemDef item = ItemCatalog.GetItemDef(itemIndex);
                    if (!item)
                        continue;

                    string qualityTierName = qualityTier.ToString().ToUpper();

                    string qualityModifierToken;
                    if (item.isConsumed)
                    {
                        qualityModifierToken = $"QUALITY_{qualityTierName}_CONSUMED_MODIFIER";
                    }
                    else
                    {
                        qualityModifierToken = $"QUALITY_{qualityTierName}_MODIFIER";
                    }

                    formatQualityNameTokens(baseItem.nameToken, item.nameToken, qualityModifierToken, qualityLanguageDictionary);

                    formatQualityTokens(baseItem.pickupToken, item.pickupToken, qualityLanguageDictionary);
                    formatQualityTokens(baseItem.descriptionToken, item.descriptionToken, qualityLanguageDictionary);
                }
            }

            foreach (EquipmentQualityGroup equipmentQualityGroup in _allEquipmentQualityGroups)
            {
                EquipmentDef baseEquipment = EquipmentCatalog.GetEquipmentDef(equipmentQualityGroup.BaseEquipmentIndex);
                if (!baseEquipment)
                {
                    Log.Error($"Invalid base equipment in group {equipmentQualityGroup}");
                    continue;
                }

                for (QualityTier qualityTier = 0; qualityTier < QualityTier.Count; qualityTier++)
                {
                    EquipmentIndex equipmentIndex = equipmentQualityGroup.GetEquipmentIndex(qualityTier);
                    EquipmentDef equipment = EquipmentCatalog.GetEquipmentDef(equipmentIndex);
                    if (!equipment)
                        continue;

                    string qualityTierName = qualityTier.ToString().ToUpper();
                    string qualityModifierToken = $"QUALITY_{qualityTierName}_MODIFIER";

                    formatQualityNameTokens(baseEquipment.nameToken, equipment.nameToken, qualityModifierToken, qualityLanguageDictionary);

                    formatQualityTokens(baseEquipment.pickupToken, equipment.pickupToken, qualityLanguageDictionary);
                    formatQualityTokens(baseEquipment.descriptionToken, equipment.descriptionToken, qualityLanguageDictionary);
                }
            }

            LanguageAPI.Add(qualityLanguageDictionary);

            foreach (Language language in tempLoadedLanguages)
            {
                language.UnloadStrings();
            }
        }

        public static QualityTierDef GetQualityTierDef(QualityTier qualityTier)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                QualityTierDef[] qualityTierDefs = new QualityTierDef[(int)QualityTier.Count];

                string[] qualityTierDefAssetGUIDs = AssetDatabase.FindAssets($"t:{nameof(QualityTierDef)}", new string[] { "Assets/ItemQualities/Assets" });
                foreach (string assetGuid in qualityTierDefAssetGUIDs)
                {
                    QualityTierDef qualityTierDef = AssetDatabase.LoadAssetAtPath<QualityTierDef>(AssetDatabase.GUIDToAssetPath(assetGuid));
                    qualityTierDefs[(int)qualityTierDef.qualityTier] = qualityTierDef;
                }

                return ArrayUtils.GetSafe(qualityTierDefs, (int)qualityTier);
            }
#endif

            return ArrayUtils.GetSafe(_qualityTierDefs, (int)qualityTier);
        }

        public static ItemQualityGroup GetItemQualityGroup(ItemQualityGroupIndex itemQualityGroupIndex)
        {
            return ArrayUtils.GetSafe(_allItemQualityGroups, (int)itemQualityGroupIndex);
        }

        public static EquipmentQualityGroup GetEquipmentQualityGroup(EquipmentQualityGroupIndex equipmentQualityGroupIndex)
        {
            return ArrayUtils.GetSafe(_allEquipmentQualityGroups, (int)equipmentQualityGroupIndex);
        }

        public static BuffQualityGroup GetBuffQualityGroup(BuffQualityGroupIndex buffQualityGroupIndex)
        {
            return ArrayUtils.GetSafe(_allBuffQualityGroups, (int)buffQualityGroupIndex);
        }

        public static ItemQualityGroupIndex FindItemQualityGroupIndex(ItemIndex itemIndex)
        {
            return ArrayUtils.GetSafe(_itemIndexToQualityGroupIndex, (int)itemIndex, ItemQualityGroupIndex.Invalid);
        }

        public static EquipmentQualityGroupIndex FindEquipmentQualityGroupIndex(EquipmentIndex equipmentIndex)
        {
            return ArrayUtils.GetSafe(_equipmentIndexToQualityGroupIndex, (int)equipmentIndex, EquipmentQualityGroupIndex.Invalid);
        }

        public static BuffQualityGroupIndex FindBuffQualityGroupIndex(BuffIndex buffIndex)
        {
            return ArrayUtils.GetSafe(_buffIndexToQualityGroupIndex, (int)buffIndex, BuffQualityGroupIndex.Invalid);
        }

        public static QualityTier GetQualityTier(ItemIndex itemIndex)
        {
            return ArrayUtils.GetSafe(_itemIndexToQuality, (int)itemIndex, QualityTier.None);
        }

        public static QualityTier GetQualityTier(EquipmentIndex equipmentIndex)
        {
            return ArrayUtils.GetSafe(_equipmentIndexToQuality, (int)equipmentIndex, QualityTier.None);
        }

        public static QualityTier GetQualityTier(BuffIndex buffIndex)
        {
            return ArrayUtils.GetSafe(_buffIndexToQuality, (int)buffIndex, QualityTier.None);
        }

        public static QualityTier GetQualityTier(PickupIndex pickupIndex)
        {
            PickupDef pickupDef = PickupCatalog.GetPickupDef(pickupIndex);
            if (pickupDef == null)
                return QualityTier.None;

            if (pickupDef.itemIndex != ItemIndex.None)
            {
                return GetQualityTier(pickupDef.itemIndex);
            }
            else if (pickupDef.equipmentIndex != EquipmentIndex.None)
            {
                return GetQualityTier(pickupDef.equipmentIndex);
            }
            else
            {
                return QualityTier.None;
            }
        }

        public static ItemIndex GetItemIndexOfQuality(ItemIndex itemIndex, QualityTier qualityTier)
        {
            ItemQualityGroup itemQualityGroup = GetItemQualityGroup(FindItemQualityGroupIndex(itemIndex));
            ItemIndex qualityItemIndex = itemQualityGroup ? itemQualityGroup.GetItemIndex(qualityTier) : ItemIndex.None;
            if (qualityItemIndex == ItemIndex.None)
            {
                if (Configs.Debug.LogItemQualities && qualityTier != QualityTier.None && itemIndex != ItemIndex.None)
                {
                    ItemDef itemDef = ItemCatalog.GetItemDef(itemIndex);
                    Log.Warning($"Item {itemDef.name} is missing quality variant {qualityTier}");
                }

                return itemIndex;
            }

            return qualityItemIndex;
        }

        public static EquipmentIndex GetEquipmentIndexOfQuality(EquipmentIndex equipmentIndex, QualityTier qualityTier)
        {
            EquipmentQualityGroup equipmentQualityGroup = GetEquipmentQualityGroup(FindEquipmentQualityGroupIndex(equipmentIndex));
            EquipmentIndex qualityEquipmentIndex = equipmentQualityGroup ? equipmentQualityGroup.GetEquipmentIndex(qualityTier) : EquipmentIndex.None;
            if (qualityEquipmentIndex == EquipmentIndex.None)
            {
                if (Configs.Debug.LogItemQualities && qualityTier != QualityTier.None && equipmentIndex != EquipmentIndex.None)
                {
                    EquipmentDef equipmentDef = EquipmentCatalog.GetEquipmentDef(equipmentIndex);
                    Log.Warning($"Equipment {equipmentDef.name} is missing quality variant {qualityTier}");
                }

                return equipmentIndex;
            }

            return qualityEquipmentIndex;
        }

        public static BuffIndex GetBuffIndexOfQuality(BuffIndex buffIndex, QualityTier qualityTier)
        {
            BuffQualityGroup buffQualityGroup = GetBuffQualityGroup(FindBuffQualityGroupIndex(buffIndex));
            BuffIndex qualityBuffIndex = buffQualityGroup ? buffQualityGroup.GetBuffIndex(qualityTier) : BuffIndex.None;
            if (qualityBuffIndex == BuffIndex.None)
            {
                if (Configs.Debug.LogItemQualities && qualityTier != QualityTier.None && buffIndex != BuffIndex.None)
                {
                    BuffDef buffDef = BuffCatalog.GetBuffDef(buffIndex);
                    Log.Warning($"Buff {buffDef.name} is missing quality variant {qualityTier}");
                }

                return buffIndex;
            }

            return qualityBuffIndex;
        }

        public static PickupIndex GetPickupIndexOfQuality(PickupIndex pickupIndex, QualityTier qualityTier)
        {
            PickupDef pickupDef = PickupCatalog.GetPickupDef(pickupIndex);
            if (pickupDef != null)
            {
                if (pickupDef.itemIndex != ItemIndex.None)
                {
                    PickupIndex qualityPickupIndex = PickupCatalog.FindPickupIndex(GetItemIndexOfQuality(pickupDef.itemIndex, qualityTier));
                    if (qualityPickupIndex != PickupIndex.none)
                    {
                        return qualityPickupIndex;
                    }
                }
                else if (pickupDef.equipmentIndex != EquipmentIndex.None)
                {
                    PickupIndex qualityPickupIndex = PickupCatalog.FindPickupIndex(GetEquipmentIndexOfQuality(pickupDef.equipmentIndex, qualityTier));
                    if (qualityPickupIndex != PickupIndex.none)
                    {
                        return qualityPickupIndex;
                    }
                }
            }

            return pickupIndex;
        }

        public static PickupIndex GetScrapIndexForPickup(PickupIndex scrappingPickupIndex)
        {
            PickupDef scrappingPickupDef = PickupCatalog.GetPickupDef(scrappingPickupIndex);
            if (scrappingPickupDef == null)
                return PickupIndex.none;

            PickupIndex scrapPickupIndex = PickupCatalog.FindScrapIndexForItemTier(scrappingPickupDef.itemTier);

            return GetPickupIndexOfQuality(scrapPickupIndex, GetQualityTier(scrappingPickupIndex));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ReadOnlyArray<ItemIndex> GetAllItemsOfQuality(QualityTier qualityTier)
        {
            return _itemsByQualityTier[(int)qualityTier + 1];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ReadOnlyArray<EquipmentIndex> GetAllEquipmentsOfQuality(QualityTier qualityTier)
        {
            return _equipmentsByQualityTier[(int)qualityTier + 1];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ReadOnlyArray<BuffIndex> GetAllBuffsOfQuality(QualityTier qualityTier)
        {
            return _buffsByQualityTier[(int)qualityTier + 1];
        }

        public static QualityTier Max(QualityTier a, QualityTier b)
        {
            return a > b ? a : b;
        }

        public static QualityTier Min(QualityTier a, QualityTier b)
        {
            return a < b ? a : b;
        }

        public static Texture2D CreateQualityIconTexture(Texture2D baseIconTexture, QualityTier qualityTier, bool useConsumedIcon = false)
        {
            return CreateQualityIconTexture(baseIconTexture, qualityTier, Color.white, useConsumedIcon);
        }

        public static Texture2D CreateQualityIconTexture(Texture2D baseIconTexture, QualityTier qualityTier, Color baseIconTint, bool useConsumedIcon = false)
        {
            return CreateQualityIconTexture(baseIconTexture, GetQualityTierDef(qualityTier), baseIconTint, useConsumedIcon);
        }

        internal static Texture2D CreateQualityIconTexture(Texture2D baseIconTexture, QualityTierDef qualityTierDef, bool useConsumedIcon = false)
        {
            return CreateQualityIconTexture(baseIconTexture, qualityTierDef, Color.white, useConsumedIcon);
        }

        internal static Texture2D CreateQualityIconTexture(Texture2D baseIconTexture, QualityTierDef qualityTierDef, Color baseIconTint, bool useConsumedIcon = false)
        {
            Texture2D iconTexture = TextureUtils.CreateAccessibleCopy(baseIconTexture);

            Sprite qualityIconSprite = qualityTierDef.icon;
            if (useConsumedIcon && qualityTierDef.consumedIcon)
            {
                qualityIconSprite = qualityTierDef.consumedIcon;
            }

            if (qualityIconSprite)
            {
                const float QualityIconRelativeSize = 0.5f;

                int width = iconTexture.width;
                int height = iconTexture.height;
                int qualityIconWidth = (int)(width * QualityIconRelativeSize);
                int qualityIconHeight = (int)(height * QualityIconRelativeSize);
                float qualityUVLeft = qualityIconSprite.rect.x / qualityIconSprite.texture.width;
                float qualityUVRight = (qualityIconSprite.rect.x + qualityIconSprite.rect.width) / qualityIconSprite.texture.width;
                float qualityUVBottom = qualityIconSprite.rect.y / qualityIconSprite.texture.height;
                float qualityUVTop = (qualityIconSprite.rect.y + qualityIconSprite.rect.height) / qualityIconSprite.texture.height;

                for (int x = 0; x < width; x++)
                {
                    for (int y = 0; y < height; y++)
                    {
                        Color pixelColor = iconTexture.GetPixel(x, y) * baseIconTint;

                        if (x < qualityIconWidth && y > height - qualityIconHeight)
                        {
                            float u = Mathf.Lerp(qualityUVLeft, qualityUVRight, (float)x / qualityIconWidth);
                            float v = Mathf.Lerp(qualityUVBottom, qualityUVTop, (float)(y - (height - qualityIconHeight)) / qualityIconHeight);
                            Color qualityIconColor = qualityIconSprite.texture.GetPixelBilinear(u, v);
                            if (qualityIconColor.a > 0)
                            {
                                pixelColor = pixelColor.a > 0 ? Color.Lerp(pixelColor, qualityIconColor, qualityIconColor.a) : qualityIconColor;
                            }
                        }

                        iconTexture.SetPixel(x, y, pixelColor);
                    }
                }

                iconTexture.Apply();
            }
            
            return iconTexture;
        }
    }
}
