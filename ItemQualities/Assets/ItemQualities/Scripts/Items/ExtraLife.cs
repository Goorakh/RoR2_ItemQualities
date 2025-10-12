using RoR2;
using System;
using UnityEngine;

namespace ItemQualities.Items
{
    static class ExtraLife
    {
        static EffectIndex _reviveEffectIndex = EffectIndex.Invalid;

        [SystemInitializer(typeof(ItemCatalog), typeof(EffectCatalogUtils))]
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
                    QualityTier.Uncommon => 2,
                    QualityTier.Rare => 4,
                    QualityTier.Epic => 6,
                    QualityTier.Legendary => 10,
                    _ => throw new NotImplementedException($"Quality tier {extraLifeQualityTier} is not implemented"),
                };

                ReviveAPI.ReviveAPI.AddCustomRevive(new ReviveAPI.ReviveAPI.CustomRevive
                {
                    priority = -(int)(qualityTier + 1),
                    canRevive = canExtraLifeQualityRevive,
                    onRevive = onRevive,
                    pendingOnRevives = new ReviveAPI.ReviveAPI.PendingOnRevive[]
                    {
                        new ReviveAPI.ReviveAPI.PendingOnRevive
                        {
                            onReviveDelegate = reviveSound,
                            timer = 1f
                        },
                        new ReviveAPI.ReviveAPI.PendingOnRevive
                        {
                            onReviveDelegate = respawnQualityExtraLife,
                            timer = 2f
                        }
                    }
                });

                bool canExtraLifeQualityRevive(CharacterMaster master)
                {
                    return master && master.inventory && master.inventory.GetItemCount(extraLifeItemIndex) > 0;
                }

                void onRevive(CharacterMaster master)
                {
                    if (master && master.inventory)
                    {
                        master.inventory.RemoveItem(extraLifeItemIndex);
                    }
                }

                void reviveSound(CharacterMaster master)
                {
                    master.PlayExtraLifeSFX();
                }

                void respawnQualityExtraLife(CharacterMaster master)
                {
                    if (!master)
                        return;

                    if (master.inventory)
                    {
                        master.inventory.GiveItem(extraLifeConsumedItemIndex);
                        CharacterMasterNotificationQueue.SendTransformNotification(master, extraLifeItemIndex, extraLifeConsumedItemIndex, CharacterMasterNotificationQueue.TransformationType.Default);
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

                    int extraLifeCount = self.inventory.GetItemCount(extraLifeItemIndex);
                    if (extraLifeCount > 0)
                    {
                        self.inventory.RemoveItem(extraLifeItemIndex, extraLifeCount);
                        self.inventory.GiveItem(extraLifeConsumedItemIndex, extraLifeCount);

                        CharacterMasterNotificationQueue.SendTransformNotification(self, extraLifeItemIndex, extraLifeConsumedItemIndex, CharacterMasterNotificationQueue.TransformationType.Default);
                    }
                }
            }

            orig(self, killerOverride, inflictorOverride, damageTypeOverride);
        }
    }
}
