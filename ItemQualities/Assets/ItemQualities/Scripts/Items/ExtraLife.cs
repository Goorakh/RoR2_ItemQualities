using Mono.Cecil.Cil;
using MonoMod.Cil;
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

            IL.RoR2.CharacterMaster.TryReviveOnBodyDeath += CharacterMaster_TryReviveOnBodyDeath;
            On.RoR2.CharacterMaster.TrueKill_GameObject_GameObject_DamageTypeCombo += CharacterMaster_TrueKill_GameObject_GameObject_DamageTypeCombo;
        }

        static void CharacterMaster_TryReviveOnBodyDeath(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            //     Inventory.ItemTransformation itemTransformation = default(Inventory.ItemTransformation);
            // IL_010C: ldloca.s  V_2
            // IL_010E: initobj   RoR2.Inventory/ItemTransformation
            //     itemTransformation.originalItemIndex = RoR2Content.Items.ExtraLife.itemIndex;
            // IL_0114: ldloca.s  V_2
            // IL_0116: ldsfld    class RoR2.ItemDef RoR2.RoR2Content/Items::ExtraLife

            int extraLifeItemTransformationVarIndex = -1;
            if (!c.TryGotoNext(MoveType.AfterLabel,
                               x => x.MatchLdloca(out extraLifeItemTransformationVarIndex),
                               x => x.MatchInitobj<Inventory.ItemTransformation>(),
                               x => x.MatchLdloca(extraLifeItemTransformationVarIndex),
                               x => x.MatchLdsfld(typeof(RoR2Content.Items), nameof(RoR2Content.Items.ExtraLife))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            ILLabel normalReviveLabel = c.DefineLabel();

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<CharacterMaster, bool>>(tryReviveQualityExtraLife);
            c.Emit(OpCodes.Brfalse, normalReviveLabel);
            c.Emit(OpCodes.Ldc_I4_1);
            c.Emit(OpCodes.Ret);

            c.MarkLabel(normalReviveLabel);

            static bool tryReviveQualityExtraLife(CharacterMaster master)
            {
                if (master && master.inventory)
                {
                    for (QualityTier qualityTier = QualityTier.Count - 1; qualityTier >= 0; qualityTier--)
                    {
                        ItemIndex qualityExtraLifeItemIndex = ItemQualitiesContent.ItemQualityGroups.ExtraLife.GetItemIndex(qualityTier);
                        if (qualityExtraLifeItemIndex == ItemIndex.None)
                            continue;

                        ItemIndex qualityExtraLifeConsumedItemIndex = ItemQualitiesContent.ItemQualityGroups.ExtraLifeConsumed.GetItemIndex(qualityTier);
                        if (qualityExtraLifeConsumedItemIndex == ItemIndex.None)
                            qualityExtraLifeConsumedItemIndex = RoR2Content.Items.ExtraLifeConsumed.itemIndex;

                        if (new Inventory.ItemTransformation
                        {
                            originalItemIndex = qualityExtraLifeItemIndex,
                            newItemIndex = qualityExtraLifeConsumedItemIndex,
                            minToTransform = 1,
                            maxToTransform = 1,
                            transformationType = (ItemTransformationTypeIndex)CharacterMasterNotificationQueue.TransformationType.Default
                        }.TryTake(master.inventory, out Inventory.ItemTransformation.TakeResult takeResult))
                        {
                            int deathEventCount;
                            switch (qualityTier)
                            {
                                case QualityTier.None:
                                    deathEventCount = 0;
                                    break;
                                case QualityTier.Uncommon:
                                    deathEventCount = 12;
                                    break;
                                case QualityTier.Rare:
                                    deathEventCount = 18;
                                    break;
                                case QualityTier.Epic:
                                    deathEventCount = 25;
                                    break;
                                case QualityTier.Legendary:
                                    deathEventCount = 30;
                                    break;
                                default:
                                    deathEventCount = 0;
                                    Log.Error($"Quality tier {qualityTier} is not implemented");
                                    break;
                            }

                            CharacterMaster.ExtraLifeServerBehavior extraLifeBehavior = master.gameObject.AddComponent<CharacterMaster.ExtraLifeServerBehavior>();
                            extraLifeBehavior.pendingTransformation = takeResult;
                            extraLifeBehavior.consumedItemIndex = qualityExtraLifeConsumedItemIndex;
                            extraLifeBehavior.completionTime = Run.FixedTimeStamp.now + 2f;
                            extraLifeBehavior.completionCallback += respawnQualityExtraLife;
                            extraLifeBehavior.soundTime = extraLifeBehavior.completionTime - 1f;
                            extraLifeBehavior.soundCallback += master.PlayExtraLifeSFX;

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

                            return true;
                        }
                    }
                }

                return false;
            }
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
