using RoR2;
using RoR2.Items;
using System;
using System.Collections;
using UnityEngine;

namespace ItemQualities.Items
{
    static class ExtraLifeVoid
    {
        static EffectIndex _reviveEffectIndex = EffectIndex.Invalid;

        [SystemInitializer(typeof(ItemCatalog), typeof(EffectCatalogUtils))]
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
                    canRevive = canExtraLifeVoidQualityRevive,
                    onRevive = onRevive,
                    pendingOnRevives = new ReviveAPI.ReviveAPI.PendingOnRevive[]
                    {
                        new ReviveAPI.ReviveAPI.PendingOnRevive
                        {
                            onReviveDelegate = reviveSoundVoid,
                            timer = 1f
                        },
                        new ReviveAPI.ReviveAPI.PendingOnRevive
                        {
                            onReviveDelegate = respawnQualityExtraLifeVoid,
                            timer = 2f
                        }
                    }
                });

                bool canExtraLifeVoidQualityRevive(CharacterMaster master)
                {
                    return master && master.inventory && master.inventory.GetItemCount(extraLifeVoidItemIndex) > 0;
                }

                void onRevive(CharacterMaster master)
                {
                    if (master && master.inventory)
                    {
                        master.inventory.RemoveItem(extraLifeVoidItemIndex);
                    }
                }

                void reviveSoundVoid(CharacterMaster master)
                {
                    master.PlayExtraLifeVoidSFX();
                }

                void respawnQualityExtraLifeVoid(CharacterMaster master)
                {
                    if (!master)
                        return;

                    if (master.inventory)
                    {
                        master.inventory.GiveItem(extraLifeVoidConsumedItemIndex);
                        CharacterMasterNotificationQueue.SendTransformNotification(master, extraLifeVoidItemIndex, extraLifeVoidConsumedItemIndex, CharacterMasterNotificationQueue.TransformationType.Default);
                    }

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

                IEnumerator waitThenCorruptItems(CharacterMaster master, QualityTier targetQualityTier)
                {
                    yield return new WaitForSeconds(ContagiousItemManager.transformDelay);

                    foreach (ContagiousItemManager.TransformationInfo transformationInfo in ContagiousItemManager.transformationInfos)
                    {
                        int originalItemCount = master.inventory.GetItemCount(transformationInfo.originalItem);
                        if (originalItemCount > 0)
                        {
                            QualityTier qualityTier = QualityCatalog.Max(QualityCatalog.GetQualityTier(transformationInfo.originalItem), targetQualityTier);
                            ItemIndex qualityTransformedItemIndex = QualityCatalog.GetItemIndexOfQuality(transformationInfo.transformedItem, qualityTier);

                            master.inventory.RemoveItem(transformationInfo.originalItem, originalItemCount);
                            master.inventory.GiveItem(qualityTransformedItemIndex, originalItemCount);

                            CharacterMasterNotificationQueue.SendTransformNotification(master, transformationInfo.originalItem, qualityTransformedItemIndex, CharacterMasterNotificationQueue.TransformationType.ContagiousVoid);
                        }

                        if (QualityCatalog.GetQualityTier(transformationInfo.transformedItem) < targetQualityTier)
                        {
                            int transformedItemCount = master.inventory.GetItemCount(transformationInfo.transformedItem);
                            if (transformedItemCount > 0)
                            {
                                ItemIndex qualityTransformedItemIndex = QualityCatalog.GetItemIndexOfQuality(transformationInfo.transformedItem, targetQualityTier);

                                master.inventory.RemoveItem(transformationInfo.transformedItem, transformedItemCount);
                                master.inventory.GiveItem(qualityTransformedItemIndex, transformedItemCount);

                                CharacterMasterNotificationQueue.SendTransformNotification(master, transformationInfo.transformedItem, qualityTransformedItemIndex, CharacterMasterNotificationQueue.TransformationType.ContagiousVoid);
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

                    int extraLifeVoidCount = self.inventory.GetItemCount(extraLifeVoidItemIndex);
                    if (extraLifeVoidCount > 0)
                    {
                        self.inventory.RemoveItem(extraLifeVoidItemIndex, extraLifeVoidCount);
                        self.inventory.GiveItem(extraLifeVoidConsumedItemIndex, extraLifeVoidCount);

                        CharacterMasterNotificationQueue.SendTransformNotification(self, extraLifeVoidItemIndex, extraLifeVoidConsumedItemIndex, CharacterMasterNotificationQueue.TransformationType.Default);
                    }
                }
            }

            orig(self, killerOverride, inflictorOverride, damageTypeOverride);
        }
    }
}
