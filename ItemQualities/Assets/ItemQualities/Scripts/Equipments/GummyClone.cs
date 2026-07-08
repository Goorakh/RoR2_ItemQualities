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
    internal static class GummyClone
    {
        private static readonly GameObject[] _qualityGummyCloneProjectilePrefabs = new GameObject[(int)QualityTier.Count];

        private static readonly Func<ItemIndex, bool>[] _qualityItemCopyFilters = new Func<ItemIndex, bool>[(int)QualityTier.Count]
        {
            uncommonItemCopyFilter,
            rareItemCopyFilter,
            epicItemCopyFilter,
            legendaryItemCopyFilter,
        };

        [ContentInitializer]
        private static IEnumerator LoadContent(ContentInitializerArgs args)
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
        private static void Init()
        {
            IL.RoR2.EquipmentSlot.FireGummyClone += EquipmentSlot_FireGummyClone;

            IL.RoR2.Projectile.GummyCloneProjectile.SpawnGummyClone += GummyCloneProjectile_SpawnGummyClone;

            IL.RoR2.CharacterMaster.SetUpGummyClone += CharacterMaster_SetUpGummyClone;
        }

        private static void CharacterMaster_SetUpGummyClone(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            /*
             *  // base.gameObject.AddComponent<MasterSuicideOnTimer>();
             *  IL_0047: ldarg.0
             *  IL_0048: call      instance class [UnityEngine.CoreModule]UnityEngine.GameObject [UnityEngine.CoreModule]UnityEngine.Component::get_gameObject()
             *  IL_004D: callvirt  instance !!0 [UnityEngine.CoreModule]UnityEngine.GameObject::AddComponent<class RoR2.MasterSuicideOnTimer>()
             */

            if (!c.TryGotoNext(MoveType.AfterLabel,
                               x => x.MatchLdarg(0),
                               x => x.MatchCallOrCallvirt<Component>("get_" + nameof(Component.gameObject)),
                               x => x.MatchCallOrCallvirt(CommonReflectionCache.AddComponent.OfType<MasterSuicideOnTimer>.Method)))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Action<CharacterMaster>>(setupGummyCloneReviveHandling);

            static void setupGummyCloneReviveHandling(CharacterMaster gummyCloneMaster)
            {
                gummyCloneMaster.EnsureComponent<ResetMasterSuicideOnTimerOnRevive>();
            }
        }

        private static void EquipmentSlot_FireGummyClone(ILContext il)
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

        private static void GummyCloneProjectile_SpawnGummyClone(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            VariableDefinition spawnCardVar = null;
            if (!c.TryGotoNext(MoveType.After,
                               x => x.MatchLdloc(typeof(MasterCopySpawnCard), il, out spawnCardVar),
                               x => x.MatchLdsfld(typeof(DLC1Content.Items), nameof(DLC1Content.Items.GummyCloneIdentifier)),
                               x => x.MatchLdcI4(out _),
                               x => x.MatchCallOrCallvirt<MasterCopySpawnCard>(nameof(MasterCopySpawnCard.GiveItem))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Emit(OpCodes.Ldarg_0);
            c.Emit(OpCodes.Ldloc, spawnCardVar);
            c.EmitDelegate<Action<GummyCloneProjectile, MasterCopySpawnCard>>(setupSpawnCard);

            static void setupSpawnCard(GummyCloneProjectile gummyCloneProjectile, MasterCopySpawnCard spawnCard)
            {
                QualityTier gummyCloneQualityTier = QualityTierContext.GetQualityTier(gummyCloneProjectile.gameObject);
                if (gummyCloneQualityTier == QualityTier.None)
                    return;

                spawnCard.GiveItem(ItemQualitiesContent.ItemQualityGroups.QualityTier.GetItemIndex(gummyCloneQualityTier));

                if (gummyCloneProjectile.TryGetComponent(out ProjectileController projectileController) &&
                    projectileController.owner &&
                    projectileController.owner.TryGetComponent(out CharacterBody ownerBody) &&
                    ownerBody.inventory)
                {
                    Func<ItemIndex, bool> itemCopyFilter = _qualityItemCopyFilters[(int)gummyCloneQualityTier];

                    bool shouldCopyItemCount = gummyCloneQualityTier == QualityTier.Legendary;

                    ReadOnlyArray<ItemIndex> allBaseQualityIems = QualityCatalog.GetAllItemsOfQuality(QualityTier.None);
                    for (int i = 0; i < allBaseQualityIems.Length; i++)
                    {
                        ItemIndex itemIndex = allBaseQualityIems[i];

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

                                        spawnCard.GiveItem(qualityItemIndex, shouldCopyItemCount ? qualityItemCountPermanent : 1);
                                    }
                                    else
                                    {
                                        qualityItemCountAccumulator += qualityItemCountPermanent;
                                    }
                                }

                                if (qualityTempItemRawValue > 0f)
                                {
                                    if (qualityItemPassesFilter)
                                    {
                                        qualityTempItemRawValueAccumulator = 0f;

                                        spawnCard.srcTempItemRawValues[(int)qualityItemIndex] += shouldCopyItemCount ? qualityTempItemRawValue : Mathf.Min(qualityTempItemRawValue, 1f);
                                    }
                                    else
                                    {
                                        qualityTempItemRawValueAccumulator += qualityTempItemRawValue;
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
                                spawnCard.GiveItem(itemIndex, shouldCopyItemCount ? itemCount : 1);
                            }

                            float tempItemRawValue = ownerBody.inventory.GetTempItemRawValue(itemIndex) + qualityTempItemRawValueAccumulator;
                            qualityTempItemRawValueAccumulator = 0f;

                            if (tempItemRawValue > 0)
                            {
                                spawnCard.srcTempItemRawValues[(int)itemIndex] += shouldCopyItemCount ? tempItemRawValue : Mathf.Min(tempItemRawValue, 1f);
                            }
                        }
                    }
                }
            }
        }

        private static bool uncommonItemCopyFilter(ItemIndex itemIndex)
        {
            if (QualityCatalog.GetQualityTier(itemIndex) > QualityTier.Uncommon)
                return false;

            ItemDef itemDef = ItemCatalog.GetItemDef(itemIndex);
            if (!itemDef)
                return false;

            if (itemDef.ContainsTag(ItemTag.CannotCopy))
                return false;

            switch (itemDef.tier)
            {
                case ItemTier.Tier1:
                    return true;
                default:
                    return false;
            }
        }

        private static bool rareItemCopyFilter(ItemIndex itemIndex)
        {
            if (QualityCatalog.GetQualityTier(itemIndex) > QualityTier.Rare)
                return false;

            ItemDef itemDef = ItemCatalog.GetItemDef(itemIndex);
            if (!itemDef)
                return false;

            if (itemDef.ContainsTag(ItemTag.CannotCopy))
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

        private static bool epicItemCopyFilter(ItemIndex itemIndex)
        {
            if (QualityCatalog.GetQualityTier(itemIndex) > QualityTier.Epic)
                return false;

            ItemDef itemDef = ItemCatalog.GetItemDef(itemIndex);
            if (!itemDef)
                return false;

            if (itemDef.ContainsTag(ItemTag.CannotCopy))
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

        private static bool legendaryItemCopyFilter(ItemIndex itemIndex)
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
