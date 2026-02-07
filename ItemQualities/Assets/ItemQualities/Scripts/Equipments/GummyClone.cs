using HG;
using ItemQualities.ContentManagement;
using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using R2API;
using RoR2;
using RoR2.Projectile;
using RoR2BepInExPack.GameAssetPathsBetter;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace ItemQualities.Equipments
{
    static class GummyClone
    {
        static readonly GameObject[] _qualityGummyCloneProjectilePrefabs = new GameObject[(int)QualityTier.Count];

        static readonly Func<ItemIndex, bool>[] _qualityItemCopyFilters = new Func<ItemIndex, bool>[(int)QualityTier.Count]
        {
            uncommonItemCopyFilter,
            rareItemCopyFilter,
            epicItemCopyFilter,
            legendaryItemCopyFilter,
        };

        [ContentInitializer]
        static IEnumerator LoadContent(ContentIntializerArgs args)
        {
            AsyncOperationHandle<GameObject> gummyCloneProjectileLoad = AddressableUtil.LoadTempAssetAsync<GameObject>(RoR2_DLC1_GummyClone.GummyCloneProjectile_prefab);
            gummyCloneProjectileLoad.OnSuccess(gummyCloneProjectilePrefab =>
            {
                for (QualityTier qualityTier = 0; qualityTier < QualityTier.Count; qualityTier++)
                {
                    GameObject qualityGummyCloneProjectilePrefab = gummyCloneProjectilePrefab.InstantiateClone(gummyCloneProjectilePrefab.name + qualityTier.ToString());

                    QualityTierContext qualityTierContext = qualityGummyCloneProjectilePrefab.AddComponent<QualityTierContext>();
                    qualityTierContext.QualityTier = qualityTier;

                    _qualityGummyCloneProjectilePrefabs[(int)qualityTier] = qualityGummyCloneProjectilePrefab;
                }

                args.ContentPack.networkedObjectPrefabs.Add(_qualityGummyCloneProjectilePrefabs);
                args.ContentPack.projectilePrefabs.Add(_qualityGummyCloneProjectilePrefabs);
            });

            return gummyCloneProjectileLoad.AsProgressCoroutine(args.ProgressReceiver);
        }

        [SystemInitializer]
        static void Init()
        {
            IL.RoR2.EquipmentSlot.FireGummyClone += EquipmentSlot_FireGummyClone;

            IL.RoR2.Projectile.GummyCloneProjectile.SpawnGummyClone += GummyCloneProjectile_SpawnGummyClone;
        }

        static void EquipmentSlot_FireGummyClone(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (!c.TryGotoNext(MoveType.After,
                               x => x.MatchLdstr("Prefabs/Projectiles/GummyCloneProjectile"),
                               x => x.MatchCallOrCallvirt(typeof(LegacyResourcesAPI), nameof(LegacyResourcesAPI.Load))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<GameObject, EquipmentSlot, GameObject>>(getProjectilePrefab);

            static GameObject getProjectilePrefab(GameObject prefab, EquipmentSlot equipmentSlot)
            {
                QualityTier qualityTier = equipmentSlot.GetCurrentEquipmentActionQualityTier();

                GameObject qualityPrefab = ArrayUtils.GetSafe(_qualityGummyCloneProjectilePrefabs, (int)qualityTier);
                return qualityPrefab ? qualityPrefab : prefab;
            }
        }

        static void GummyCloneProjectile_SpawnGummyClone(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            int spawnCardVarIndex = -1;
            if (!c.TryGotoNext(MoveType.After,
                               x => x.MatchLdloc(typeof(MasterCopySpawnCard), il, out spawnCardVarIndex),
                               x => x.MatchLdsfld(typeof(DLC1Content.Items), nameof(DLC1Content.Items.GummyCloneIdentifier)),
                               x => x.MatchLdcI4(out _),
                               x => x.MatchCallOrCallvirt<MasterCopySpawnCard>(nameof(MasterCopySpawnCard.GiveItem))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Emit(OpCodes.Ldarg_0);
            c.Emit(OpCodes.Ldloc, spawnCardVarIndex);
            c.EmitDelegate<Action<GummyCloneProjectile, MasterCopySpawnCard>>(setupSpawnCard);

            static void setupSpawnCard(GummyCloneProjectile gummyCloneProjectile, MasterCopySpawnCard spawnCard)
            {
                QualityTier gummyCloneQualityTier = QualityTierContext.GetQualityTier(gummyCloneProjectile.gameObject);
                if (gummyCloneQualityTier == QualityTier.None)
                    return;

                if (gummyCloneProjectile.TryGetComponent(out ProjectileController projectileController) &&
                    projectileController.owner &&
                    projectileController.owner.TryGetComponent(out CharacterBody ownerBody) &&
                    ownerBody.inventory)
                {
                    Func<ItemIndex, bool> itemCopyFilter = _qualityItemCopyFilters[(int)gummyCloneQualityTier];

                    ReadOnlyArray<ItemIndex> allBaseQUulityIems = QualityCatalog.GetAllItemsOfQuality(QualityTier.None);
                    for (int i = 0; i < allBaseQUulityIems.Length; i++)
                    {
                        ItemIndex itemIndex = allBaseQUulityIems[i];

                        int qualityItemCountAccumulator = 0;
                        float qualityTempItemRawValueAccumulator = 0f;

                        for (QualityTier qualityTier = QualityTier.Count - 1; qualityTier >= 0; qualityTier--)
                        {
                            ItemIndex qualityItemIndex = QualityCatalog.GetItemIndexOfQuality(itemIndex, qualityTier);
                            if (qualityItemIndex == itemIndex)
                                continue;
                            
                            int qualityItemCountPermanent = ownerBody.inventory.GetItemCountPermanent(qualityItemIndex) + qualityItemCountAccumulator;
                            float qualityTempItemRawValue = ownerBody.inventory.GetTempItemRawValue(qualityItemIndex) + qualityTempItemRawValueAccumulator;

                            if (qualityItemCountPermanent > 0 || qualityTempItemRawValue > 0f)
                            {
                                bool qualityItemPassesFilter = itemCopyFilter(qualityItemIndex);

                                if (qualityItemCountPermanent > 0)
                                {
                                    if (qualityItemPassesFilter)
                                    {
                                        qualityItemCountAccumulator = 0;

                                        spawnCard.GiveItem(qualityItemIndex);
                                    }
                                    else
                                    {
                                        qualityItemCountAccumulator = qualityItemCountPermanent;
                                    }
                                }

                                if (qualityTempItemRawValue > 0f)
                                {
                                    if (qualityItemPassesFilter)
                                    {
                                        qualityTempItemRawValueAccumulator = 0f;

                                        spawnCard.srcTempItemRawValues[(int)qualityItemIndex] += Mathf.Min(qualityTempItemRawValue, 1f);
                                    }
                                    else
                                    {
                                        qualityTempItemRawValueAccumulator = qualityTempItemRawValue;
                                    }
                                }
                            }
                        }

                        if (itemCopyFilter(itemIndex))
                        {
                            int itemCount = ownerBody.inventory.GetItemCountPermanent(itemIndex) + qualityItemCountAccumulator;
                            qualityItemCountAccumulator = 0;

                            if (itemCount > 0)
                            {
                                spawnCard.GiveItem(itemIndex);
                            }

                            float tempItemRawValue = ownerBody.inventory.GetTempItemRawValue(itemIndex) + qualityTempItemRawValueAccumulator;
                            qualityTempItemRawValueAccumulator = 0f;

                            if (tempItemRawValue > 0)
                            {
                                spawnCard.srcTempItemRawValues[(int)itemIndex] += Mathf.Min(tempItemRawValue, 1f);
                            }
                        }
                    }

                    int equipmentSlotCount = ownerBody.inventory.GetEquipmentSlotCount();
                    Array.Resize(ref spawnCard.srcEquipment, equipmentSlotCount);

                    for (uint slot = 0; slot < equipmentSlotCount; slot++)
                    {
                        int equipmentSetCount = ownerBody.inventory.GetEquipmentSetCount(slot);

                        Array.Resize(ref spawnCard.srcEquipment[slot], equipmentSetCount);

                        for (uint set = 0; set < equipmentSetCount; set++)
                        {
                            EquipmentIndex equipmentIndex = ownerBody.inventory.GetEquipment(slot, set).equipmentIndex;
                            QualityTier equipmentQualityTier = QualityCatalog.GetQualityTier(equipmentIndex);

                            if (equipmentQualityTier > gummyCloneQualityTier)
                            {
                                equipmentIndex = QualityCatalog.GetEquipmentIndexOfQuality(equipmentIndex, gummyCloneQualityTier);
                                equipmentQualityTier = gummyCloneQualityTier;
                            }

                            spawnCard.srcEquipment[slot][set] = equipmentIndex;
                        }
                    }
                }
            }
        }

        static bool uncommonItemCopyFilter(ItemIndex itemIndex)
        {
            if (QualityCatalog.GetQualityTier(itemIndex) > QualityTier.Uncommon)
                return false;

            ItemDef itemDef = ItemCatalog.GetItemDef(itemIndex);
            if (!itemDef)
                return false;

            if (itemDef.ContainsTag(ItemTag.CannotCopy))
                return false;

            if (itemDef.DoesNotContainTag(ItemTag.Utility))
                return false;

            switch (itemDef.tier)
            {
                case ItemTier.Tier1:
                    return true;
                default:
                    return false;
            }
        }

        static bool rareItemCopyFilter(ItemIndex itemIndex)
        {
            if (QualityCatalog.GetQualityTier(itemIndex) > QualityTier.Rare)
                return false;

            ItemDef itemDef = ItemCatalog.GetItemDef(itemIndex);
            if (!itemDef)
                return false;

            if (itemDef.ContainsTag(ItemTag.CannotCopy))
                return false;

            if (itemDef.DoesNotContainTag(ItemTag.Utility) && itemDef.DoesNotContainTag(ItemTag.Healing))
                return false;

            switch (itemDef.tier)
            {
                case ItemTier.Tier1:
                case ItemTier.Tier2:
                    return true;
                default:
                    return false;
            }
        }

        static bool epicItemCopyFilter(ItemIndex itemIndex)
        {
            if (QualityCatalog.GetQualityTier(itemIndex) > QualityTier.Epic)
                return false;

            ItemDef itemDef = ItemCatalog.GetItemDef(itemIndex);
            if (!itemDef)
                return false;

            if (itemDef.ContainsTag(ItemTag.CannotCopy))
                return false;

            if (itemDef.DoesNotContainTag(ItemTag.Utility) && itemDef.DoesNotContainTag(ItemTag.Healing) && itemDef.DoesNotContainTag(ItemTag.Damage))
                return false;

            switch (itemDef.tier)
            {
                case ItemTier.Tier1:
                case ItemTier.Tier2:
                case ItemTier.Tier3:
                    return true;
                default:
                    return false;
            }
        }

        static bool legendaryItemCopyFilter(ItemIndex itemIndex)
        {
            ItemDef itemDef = ItemCatalog.GetItemDef(itemIndex);
            if (!itemDef)
                return false;

            if (itemDef.ContainsTag(ItemTag.CannotCopy))
                return false;

            return true;
        }
    }
}
