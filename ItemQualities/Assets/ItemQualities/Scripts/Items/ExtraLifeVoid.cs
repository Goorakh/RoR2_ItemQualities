using RoR2;
using RoR2.Items;
using System.Collections;
using UnityEngine;

namespace ItemQualities.Items
{
    static class ExtraLifeVoid
    {
        static EffectIndex _reviveEffectIndex = EffectIndex.Invalid;

        [SystemInitializer(typeof(QualityCatalog), typeof(EffectCatalogUtils))]
        static void Init()
        {
            _reviveEffectIndex = EffectCatalogUtils.FindEffectIndex("VoidRezEffect");
            if (_reviveEffectIndex == EffectIndex.Invalid)
            {
                Log.Warning("Failed to find revive effect index");
            }

            for (QualityTier qualityTier = 0; qualityTier < QualityTier.Count; qualityTier++)
            {
                QualityTier extraLifeVoidQualityTier = qualityTier;

                ItemIndex extraLifeVoidItemIndex = ItemQualitiesContent.ItemQualityGroups.ExtraLifeVoid.GetItemIndex(extraLifeVoidQualityTier);
                ItemIndex extraLifeVoidConsumedItemIndex = ItemQualitiesContent.ItemQualityGroups.ExtraLifeVoidConsumed.GetItemIndex(extraLifeVoidQualityTier);

                ReviveAPI.ReviveAPI.AddCustomRevive(new ReviveAPI.ReviveAPI.CustomRevive
                {
                    priority = -(int)(qualityTier + 1),
                    canReviveNew = canRevive,
                    onReviveNew = onRevive
                });

                bool canRevive(CharacterMaster master, out ReviveAPI.ReviveAPI.CanReviveInfo info)
                {
                    Inventory.ItemTransformation itemTransformation = new Inventory.ItemTransformation
                    {
                        originalItemIndex = extraLifeVoidItemIndex,
                        newItemIndex = extraLifeVoidConsumedItemIndex,
                        transformationType = (ItemTransformationTypeIndex)CharacterMasterNotificationQueue.TransformationType.Default
                    };

                    if (itemTransformation.CanTake(master.inventory, out Inventory.ItemTransformation.CanTakeResult canTakeResult))
                    {
                        info = new ReviveAPI.ReviveAPI.CanReviveInfo { canTakeResult = canTakeResult };
                        return true;
                    }

                    info = null;
                    return false;
                }

                void onRevive(CharacterMaster master, ReviveAPI.ReviveAPI.CanReviveInfo info)
                {
                    Inventory.ItemTransformation.TakeResult takeResult = info.canTakeResult.PerformTake();
                    CharacterMaster.ExtraLifeServerBehavior extraLifeBehavior = master.gameObject.AddComponent<CharacterMaster.ExtraLifeServerBehavior>();
                    extraLifeBehavior.pendingTransformation = takeResult;
                    extraLifeBehavior.consumedItemIndex = extraLifeVoidConsumedItemIndex;
                    extraLifeBehavior.completionTime = Run.FixedTimeStamp.now + 2f;
                    extraLifeBehavior.completionCallback += respawnQualityExtraLifeVoid;
                    extraLifeBehavior.soundTime = extraLifeBehavior.completionTime - 1f;
                    extraLifeBehavior.soundCallback += reviveSoundVoid;

                    void reviveSoundVoid()
                    {
                        master.PlayExtraLifeVoidSFX();
                    }

                    void respawnQualityExtraLifeVoid()
                    {
                        if (!master)
                            return;

                        Vector3 reviveFootPosition = master.deathFootPosition;
                        if (master.killedByUnsafeArea)
                        {
                            reviveFootPosition = TeleportHelper.FindSafeTeleportDestination(master.deathFootPosition, master.bodyPrefab.GetComponent<CharacterBody>(), RoR2Application.rng) ?? master.deathFootPosition;
                        }

                        CharacterBody body = master.Respawn(reviveFootPosition, Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f), true);
                        body.AddTimedBuff(RoR2Content.Buffs.Immune, 3f);

                        foreach (EntityStateMachine entityStateMachine in body.GetComponents<EntityStateMachine>())
                        {
                            entityStateMachine.initialStateType = entityStateMachine.mainStateType;
                        }

                        if (_reviveEffectIndex != EffectIndex.Invalid)
                        {
                            EffectManager.SpawnEffect(_reviveEffectIndex, new EffectData
                            {
                                origin = reviveFootPosition,
                                rotation = body.transform.rotation
                            }, true);
                        }

                        master.StartCoroutine(waitThenCorruptItems(master, extraLifeVoidQualityTier));
                    }

                    static IEnumerator waitThenCorruptItems(CharacterMaster master, QualityTier targetQualityTier)
                    {
                        yield return new WaitForSeconds(ContagiousItemManager.transformDelay);
                        corruptItems(master, targetQualityTier);
                    }

                    static void corruptItems(CharacterMaster master, QualityTier targetQualityTier)
                    {
                        for (int i = 0; i < ContagiousItemManager.transformationInfos.Length; i++)
                        {
                            ref readonly ContagiousItemManager.TransformationInfo transformationInfo = ref ContagiousItemManager.transformationInfos[i];

                            QualityTier corruptedQualityTier = QualityCatalog.Max(QualityCatalog.GetQualityTier(transformationInfo.originalItem), targetQualityTier);
                            ItemIndex qualityTransformedItemIndex = QualityCatalog.GetItemIndexOfQuality(transformationInfo.transformedItem, corruptedQualityTier);

                            Inventory.ItemTransformation corruptItemTransformation = new Inventory.ItemTransformation
                            {
                                originalItemIndex = transformationInfo.originalItem,
                                newItemIndex = qualityTransformedItemIndex,
                                maxToTransform = int.MaxValue,
                                transformationType = (ItemTransformationTypeIndex)CharacterMasterNotificationQueue.TransformationType.ContagiousVoid
                            };

                            corruptItemTransformation.TryTransform(master.inventory, out _);

                            if (QualityCatalog.GetQualityTier(transformationInfo.transformedItem) < targetQualityTier)
                            {
                                ItemIndex upgradedTransformedItemIndex = QualityCatalog.GetItemIndexOfQuality(transformationInfo.transformedItem, targetQualityTier);
                                if (upgradedTransformedItemIndex != transformationInfo.transformedItem)
                                {
                                    Inventory.ItemTransformation transformedItemUpgrade = new Inventory.ItemTransformation
                                    {
                                        originalItemIndex = transformationInfo.transformedItem,
                                        newItemIndex = upgradedTransformedItemIndex,
                                        maxToTransform = int.MaxValue,
                                        transformationType = (ItemTransformationTypeIndex)CharacterMasterNotificationQueue.TransformationType.ContagiousVoid
                                    };

                                    transformedItemUpgrade.TryTransform(master.inventory, out _);
                                }
                            }
                        }
                    }
                }
            }

            On.RoR2.CharacterMaster.TrueKill_GameObject_GameObject_DamageTypeCombo += CharacterMaster_TrueKill_GameObject_GameObject_DamageTypeCombo;
        }

        static void CharacterMaster_TrueKill_GameObject_GameObject_DamageTypeCombo(On.RoR2.CharacterMaster.orig_TrueKill_GameObject_GameObject_DamageTypeCombo orig, CharacterMaster self, GameObject killerOverride, GameObject inflictorOverride, DamageTypeCombo damageTypeOverride)
        {
            if (self && self.inventory)
            {
                for (QualityTier qualityTier = 0; qualityTier < QualityTier.Count; qualityTier++)
                {
                    ItemIndex extraLifeVoidItemIndex = ItemQualitiesContent.ItemQualityGroups.ExtraLifeVoid.GetItemIndex(qualityTier);
                    ItemIndex extraLifeVoidConsumedItemIndex = ItemQualitiesContent.ItemQualityGroups.ExtraLifeVoidConsumed.GetItemIndex(qualityTier);

                    Inventory.ItemTransformation consumeItemTransformation = new Inventory.ItemTransformation
                    {
                        originalItemIndex = extraLifeVoidItemIndex,
                        newItemIndex = extraLifeVoidConsumedItemIndex,
                        allowWhenDisabled = true,
                        maxToTransform = int.MaxValue,
                        transformationType = (ItemTransformationTypeIndex)CharacterMasterNotificationQueue.TransformationType.Default
                    };

                    consumeItemTransformation.TryTransform(self.inventory, out _);
                }
            }

            orig(self, killerOverride, inflictorOverride, damageTypeOverride);
        }
    }
}
