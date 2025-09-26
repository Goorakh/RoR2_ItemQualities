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
using UnityEngine.ResourceManagement.AsyncOperations;

namespace ItemQualities
{
    public static class QualityCatalog
    {
        static QualityTierDef[] _qualityTierDefs = new QualityTierDef[(int)QualityTier.Count];

        static ItemQualityGroup[] _allItemQualityGroups = Array.Empty<ItemQualityGroup>();
        static QualityTier[] _itemIndexToQuality = Array.Empty<QualityTier>();
        static ItemQualityGroupIndex[] _itemIndexToQualityGroupIndex = Array.Empty<ItemQualityGroupIndex>();

        static EquipmentQualityGroup[] _allEquipmentQualityGroups = Array.Empty<EquipmentQualityGroup>();
        static QualityTier[] _equipmentIndexToQuality = Array.Empty<QualityTier>();
        static EquipmentQualityGroupIndex[] _equipmentIndexToQualityGroupIndex = Array.Empty<EquipmentQualityGroupIndex>();

        public static int ItemQualityGroupCount => _allItemQualityGroups.Length;

        public static int EquipmentQualityGroupCount => _allEquipmentQualityGroups.Length;

        public static ResourceAvailability Availability = new ResourceAvailability();

        [SystemInitializer(typeof(ItemCatalog), typeof(EquipmentCatalog))]
        static IEnumerator Init()
        {
            yield return SetQualityGroups(ItemQualitiesContent.QualityTiers.AllQualityTiers, ItemQualitiesContent.ItemQualityGroups.AllGroups, ItemQualitiesContent.EquipmentQualityGroups.AllGroups);

            Availability.MakeAvailable();
        }

        static IEnumerator SetQualityGroups(IReadOnlyCollection<QualityTierDef> qualityTierDefs, IReadOnlyCollection<ItemQualityGroup> itemQualityGroups, IReadOnlyCollection<EquipmentQualityGroup> equipmentQualityGroups)
        {
            foreach (QualityTierDef qualityTierDef in qualityTierDefs)
            {
                _qualityTierDefs[(int)qualityTierDef.qualityTier] = qualityTierDef;
            }

            foreach (ItemQualityGroup itemQualityGroup in _allItemQualityGroups)
            {
                itemQualityGroup.GroupIndex = ItemQualityGroupIndex.Invalid;
            }

            _allItemQualityGroups = itemQualityGroups.ToArray();
            Array.Sort(_allItemQualityGroups, (a, b) => string.Compare(a.name, b.name));

            Array.Resize(ref _itemIndexToQuality, ItemCatalog.itemCount);
            Array.Fill(_itemIndexToQuality, QualityTier.None);

            Array.Resize(ref _itemIndexToQualityGroupIndex, ItemCatalog.itemCount);
            Array.Fill(_itemIndexToQualityGroupIndex, ItemQualityGroupIndex.Invalid);

            foreach (EquipmentQualityGroup equipmentQualityGroup in _allEquipmentQualityGroups)
            {
                equipmentQualityGroup.GroupIndex = EquipmentQualityGroupIndex.Invalid;
            }

            _allEquipmentQualityGroups = equipmentQualityGroups.ToArray();
            Array.Sort(_allEquipmentQualityGroups, (a, b) => string.Compare(a.name, b.name));

            Array.Resize(ref _equipmentIndexToQuality, EquipmentCatalog.equipmentCount);
            Array.Fill(_equipmentIndexToQuality, QualityTier.None);

            Array.Resize(ref _equipmentIndexToQualityGroupIndex, EquipmentCatalog.equipmentCount);
            Array.Fill(_equipmentIndexToQualityGroupIndex, EquipmentQualityGroupIndex.Invalid);

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
                    
                    _itemIndexToQuality[(int)itemIndex] = qualityTier;
                    _itemIndexToQualityGroupIndex[(int)itemIndex] = itemQualityGroupIndex;
                }

                for (QualityTier qualityTier = 0; qualityTier < QualityTier.Count; qualityTier++)
                {
                    recordItemInGroup(itemQualityGroup.GetItemIndex(qualityTier), qualityTier);
                }

                if (itemQualityGroup.BaseItemReference != null && itemQualityGroup.BaseItemReference.RuntimeKeyIsValid())
                {
                    AsyncOperationHandle<ItemDef> baseItemLoad = AddressableUtil.LoadTempAssetAsync(itemQualityGroup.BaseItemReference);
                    baseItemLoad.OnSuccess(baseItem =>
                    {
                        if (baseItem.itemIndex != ItemIndex.None)
                        {
                            itemQualityGroup.BaseItemIndex = baseItem.itemIndex;
                            _itemIndexToQualityGroupIndex[(int)baseItem.itemIndex] = itemQualityGroupIndex;
                        }
                    });

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

                    _equipmentIndexToQuality[(int)equipmentIndex] = qualityTier;
                    _equipmentIndexToQualityGroupIndex[(int)equipmentIndex] = equipmentQualityGroupIndex;
                }

                for (QualityTier qualityTier = 0; qualityTier < QualityTier.Count; qualityTier++)
                {
                    recordEquipmentInGroup(equipmentQualityGroup.GetEquipmentIndex(qualityTier), qualityTier);
                }

                if (equipmentQualityGroup.BaseEquipmentReference != null && equipmentQualityGroup.BaseEquipmentReference.RuntimeKeyIsValid())
                {
                    AsyncOperationHandle<EquipmentDef> baseEquipmentLoad = AddressableUtil.LoadTempAssetAsync(equipmentQualityGroup.BaseEquipmentReference);
                    baseEquipmentLoad.OnSuccess(baseEquipment =>
                    {
                        if (baseEquipment.equipmentIndex != EquipmentIndex.None)
                        {
                            equipmentQualityGroup.BaseEquipmentIndex = baseEquipment.equipmentIndex;
                            _equipmentIndexToQualityGroupIndex[(int)baseEquipment.equipmentIndex] = equipmentQualityGroupIndex;
                        }
                    });

                    baseAssetsParallelLoadCoroutine.Add(baseEquipmentLoad);
                }
            }

            yield return baseAssetsParallelLoadCoroutine;

            foreach (ItemQualityGroup itemQualityGroup in _allItemQualityGroups)
            {
                ItemDef baseItem = ItemCatalog.GetItemDef(itemQualityGroup.BaseItemIndex);
                if (!baseItem)
                {
                    Log.Error($"Invalid base item in group {itemQualityGroup}");
                    continue;
                }

                void resolveLanguageToken(ItemIndex itemIndex, QualityTier qualityTier)
                {
                    ItemDef item = ItemCatalog.GetItemDef(itemIndex);
                    if (!item)
                        return;

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

                    if (!string.IsNullOrEmpty(item.nameToken) && Language.IsTokenInvalid(item.nameToken))
                    {
                        foreach (Language language in Language.GetAllLanguages())
                        {
                            string generatedQualityName = language.GetLocalizedFormattedStringByToken(qualityModifierToken, language.GetLocalizedStringByToken(baseItem.nameToken));
                            LanguageAPI.Add(item.nameToken, generatedQualityName, language.name);
                        }
                    }

                    if (!string.IsNullOrEmpty(item.pickupToken) && !Language.IsTokenInvalid(item.pickupToken))
                    {
                        foreach (Language language in Language.GetAllLanguages())
                        {
                            string pickupString = language.GetLocalizedStringByToken(item.pickupToken);

                            if (pickupString.Contains("{0}"))
                            {
                                item.pickupToken += "_GEN";

                                pickupString = string.Format(pickupString, language.GetLocalizedStringByToken(baseItem.pickupToken));
                                LanguageAPI.Add(item.pickupToken, pickupString, language.name);
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(item.descriptionToken) && !Language.IsTokenInvalid(item.descriptionToken))
                    {
                        foreach (Language language in Language.GetAllLanguages())
                        {
                            string pickupString = language.GetLocalizedStringByToken(item.descriptionToken);

                            if (pickupString.Contains("{0}"))
                            {
                                item.descriptionToken += "_GEN";

                                pickupString = string.Format(pickupString, language.GetLocalizedStringByToken(baseItem.descriptionToken));
                                LanguageAPI.Add(item.descriptionToken, pickupString, language.name);
                            }
                        }
                    }
                }

                for (QualityTier qualityTier = 0; qualityTier < QualityTier.Count; qualityTier++)
                {
                    resolveLanguageToken(itemQualityGroup.GetItemIndex(qualityTier), qualityTier);
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

                void resolveLanguageToken(EquipmentIndex equipmentIndex, QualityTier qualityTier)
                {
                    EquipmentDef equipment = EquipmentCatalog.GetEquipmentDef(equipmentIndex);
                    if (!equipment)
                        return;

                    string qualityTierName = qualityTier.ToString().ToUpper();
                    string qualityModifierToken = $"QUALITY_{qualityTierName}_MODIFIER";

                    if (!string.IsNullOrEmpty(equipment.nameToken) && Language.IsTokenInvalid(equipment.nameToken))
                    {
                        foreach (Language language in Language.GetAllLanguages())
                        {
                            string generatedQualityName = language.GetLocalizedFormattedStringByToken(qualityModifierToken, language.GetLocalizedStringByToken(baseEquipment.nameToken));
                            LanguageAPI.Add(equipment.nameToken, generatedQualityName, language.name);
                        }
                    }

                    if (!string.IsNullOrEmpty(equipment.pickupToken) && !Language.IsTokenInvalid(equipment.pickupToken))
                    {
                        foreach (Language language in Language.GetAllLanguages())
                        {
                            string pickupString = language.GetLocalizedStringByToken(equipment.pickupToken);

                            if (pickupString.Contains("{0}"))
                            {
                                equipment.pickupToken += "_GEN";

                                pickupString = string.Format(pickupString, language.GetLocalizedStringByToken(baseEquipment.pickupToken));
                                LanguageAPI.Add(equipment.pickupToken, pickupString, language.name);
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(equipment.descriptionToken) && !Language.IsTokenInvalid(equipment.descriptionToken))
                    {
                        foreach (Language language in Language.GetAllLanguages())
                        {
                            string pickupString = language.GetLocalizedStringByToken(equipment.descriptionToken);

                            if (pickupString.Contains("{0}"))
                            {
                                equipment.descriptionToken += "_GEN";

                                pickupString = string.Format(pickupString, language.GetLocalizedStringByToken(baseEquipment.descriptionToken));
                                LanguageAPI.Add(equipment.descriptionToken, pickupString, language.name);
                            }
                        }
                    }
                }

                for (QualityTier qualityTier = 0; qualityTier < QualityTier.Count; qualityTier++)
                {
                    resolveLanguageToken(equipmentQualityGroup.GetEquipmentIndex(qualityTier), qualityTier);
                }
            }
        }

        public static QualityTierDef GetQualityTierDef(QualityTier qualityTier)
        {
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

        public static ItemQualityGroupIndex FindItemQualityGroupIndex(ItemIndex itemIndex)
        {
            return ArrayUtils.GetSafe(_itemIndexToQualityGroupIndex, (int)itemIndex, ItemQualityGroupIndex.Invalid);
        }

        public static EquipmentQualityGroupIndex FindEquipmentQualityGroupIndex(EquipmentIndex equipmentIndex)
        {
            return ArrayUtils.GetSafe(_equipmentIndexToQualityGroupIndex, (int)equipmentIndex, EquipmentQualityGroupIndex.Invalid);
        }

        public static QualityTier GetQualityTier(ItemIndex itemIndex)
        {
            return ArrayUtils.GetSafe(_itemIndexToQuality, (int)itemIndex, QualityTier.None);
        }

        public static QualityTier GetQualityTier(EquipmentIndex equipmentIndex)
        {
            return ArrayUtils.GetSafe(_equipmentIndexToQuality, (int)equipmentIndex, QualityTier.None);
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
            if (!itemQualityGroup)
                return itemIndex;

            ItemIndex qualityItemIndex = itemQualityGroup.GetItemIndex(qualityTier);
            if (qualityItemIndex == ItemIndex.None)
                return itemIndex;

            return qualityItemIndex;
        }

        public static EquipmentIndex GetEquipmentIndexOfQuality(EquipmentIndex equipmentIndex, QualityTier qualityTier)
        {
            EquipmentQualityGroup equipmentQualityGroup = GetEquipmentQualityGroup(FindEquipmentQualityGroupIndex(equipmentIndex));
            if (!equipmentQualityGroup)
                return equipmentIndex;

            EquipmentIndex qualityEquipmentIndex = equipmentQualityGroup.GetEquipmentIndex(qualityTier);
            if (qualityEquipmentIndex == EquipmentIndex.None)
                return equipmentIndex;

            return qualityEquipmentIndex;
        }

        public static PickupIndex GetPickupIndexOfQuality(PickupIndex pickupIndex, QualityTier qualityTier)
        {
            PickupDef pickupDef = PickupCatalog.GetPickupDef(pickupIndex);
            if (pickupDef == null)
                return pickupIndex;

            if (pickupDef.itemIndex != ItemIndex.None)
            {
                PickupIndex qualityPickupIndex = PickupCatalog.FindPickupIndex(GetItemIndexOfQuality(pickupDef.itemIndex, qualityTier));
                if (qualityPickupIndex != PickupIndex.none)
                {
                    pickupIndex = qualityPickupIndex;
                }
            }
            else if (pickupDef.equipmentIndex != EquipmentIndex.None)
            {
                PickupIndex qualityPickupIndex = PickupCatalog.FindPickupIndex(GetEquipmentIndexOfQuality(pickupDef.equipmentIndex, qualityTier));
                if (qualityPickupIndex != PickupIndex.none)
                {
                    pickupIndex = qualityPickupIndex;
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
    }
}
