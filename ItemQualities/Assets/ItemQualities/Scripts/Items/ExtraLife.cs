using RoR2;
using System;
using UnityEngine;

namespace ItemQualities.Items
{
    static class ExtraLife
    {
        static EffectIndex _reviveEffectIndex = EffectIndex.Invalid;

        [SystemInitializer(typeof(QualityCatalog), typeof(EffectCatalogUtils))]
        static void Init()
        {
            _reviveEffectIndex = EffectCatalogUtils.FindEffectIndex("HippoRezEffect");
            if (_reviveEffectIndex == EffectIndex.Invalid)
            {
                Log.Warning("Failed to find revive effect index");
            }

            for (QualityTier qualityTier = 0; qualityTier < QualityTier.Count; qualityTier++)
            {
                QualityTier extraLifeQualityTier = qualityTier;

                ItemIndex extraLifeItemIndex = ItemQualitiesContent.ItemQualityGroups.ExtraLife.GetItemIndex(extraLifeQualityTier);
                ItemIndex extraLifeConsumedItemIndex = ItemQualitiesContent.ItemQualityGroups.ExtraLifeConsumed.GetItemIndex(extraLifeQualityTier);

                int deathEventCount = extraLifeQualityTier switch
                {
                    QualityTier.Uncommon => 12,
                    QualityTier.Rare => 18,
                    QualityTier.Epic => 25,
                    QualityTier.Legendary => 30,
                    _ => throw new NotImplementedException($"Quality tier {extraLifeQualityTier} is not implemented"),
                };

                ReviveAPI.ReviveAPI.AddCustomRevive(new ReviveAPI.ReviveAPI.CustomRevive
                {
                    priority = -(int)(qualityTier + 1),
                    canRevive = canRevive,
                    onRevive = onRevive
                });

                Inventory.ItemTransformation getConsumeExtraLifeItemTransformation()
                {
                    return new Inventory.ItemTransformation
                    {
                        originalItemIndex = extraLifeItemIndex,
                        newItemIndex = extraLifeConsumedItemIndex,
                        transformationType = (ItemTransformationTypeIndex)CharacterMasterNotificationQueue.TransformationType.Default
                    };
                }

                bool canRevive(CharacterMaster master)
                {
                    Inventory.ItemTransformation itemTransformation = getConsumeExtraLifeItemTransformation();

                    return itemTransformation.CanTake(master.inventory, out _);
                }

                void onRevive(CharacterMaster master)
                {
                    Inventory.ItemTransformation itemTransformation = getConsumeExtraLifeItemTransformation();

                    if (itemTransformation.TryTake(master.inventory, out Inventory.ItemTransformation.TakeResult takeResult))
                    {
                        CharacterMaster.ExtraLifeServerBehavior extraLifeBehavior = master.gameObject.AddComponent<CharacterMaster.ExtraLifeServerBehavior>();
                        extraLifeBehavior.pendingTransformation = takeResult;
                        extraLifeBehavior.consumedItemIndex = itemTransformation.newItemIndex;
                        extraLifeBehavior.completionTime = Run.FixedTimeStamp.now + 2f;
                        extraLifeBehavior.soundCallback += reviveSound;
                        extraLifeBehavior.completionCallback += respawnQualityExtraLife;
                    }

                    void reviveSound()
                    {
                        master.PlayExtraLifeSFX();
                    }

                    void respawnQualityExtraLife()
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

                        if (deathEventCount > 0)
                        {
                            GameObject reviveAttachmentObj = GameObject.Instantiate(ItemQualitiesContent.NetworkedPrefabs.ExtraLifeReviveAttachment);

                            RepeatDeathEvent repeatDeathEvent = reviveAttachmentObj.GetComponent<RepeatDeathEvent>();
                            repeatDeathEvent.RemainingDeathEvents = deathEventCount;

                            NetworkedBodyAttachment reviveAttachment = reviveAttachmentObj.GetComponent<NetworkedBodyAttachment>();
                            reviveAttachment.AttachToGameObjectAndSpawn(body.gameObject);
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
                    ItemIndex extraLifeItemIndex = ItemQualitiesContent.ItemQualityGroups.ExtraLife.GetItemIndex(qualityTier);
                    ItemIndex extraLifeConsumedItemIndex = ItemQualitiesContent.ItemQualityGroups.ExtraLifeConsumed.GetItemIndex(qualityTier);

                    Inventory.ItemTransformation consumeItemTransformation = new Inventory.ItemTransformation
                    {
                        originalItemIndex = extraLifeItemIndex,
                        newItemIndex = extraLifeConsumedItemIndex,
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
