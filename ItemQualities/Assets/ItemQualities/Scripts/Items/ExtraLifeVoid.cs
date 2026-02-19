using Mono.Cecil.Cil;
using MonoMod.Cil;
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

        [SystemInitializer(typeof(QualityCatalog), typeof(EffectCatalogUtils))]
        static void Init()
        {
            _reviveEffectIndex = EffectCatalogUtils.FindEffectIndex("VoidRezEffect");
            if (_reviveEffectIndex == EffectIndex.Invalid)
            {
                Log.Warning("Failed to find revive effect index");
            }

            IL.RoR2.CharacterMaster.TryReviveOnBodyDeath += CharacterMaster_TryReviveOnBodyDeath;
            On.RoR2.CharacterMaster.TrueKill_GameObject_GameObject_DamageTypeCombo += CharacterMaster_TrueKill_GameObject_GameObject_DamageTypeCombo;
        }

        private static void CharacterMaster_TryReviveOnBodyDeath(ILContext il)
        {

            ILCursor c = new ILCursor(il);

            //      itemTransformation = default(Inventory.ItemTransformation);.ItemTransformation);
            // IL_01C1: ldloca.s  V_2
            // IL_01C3: initobj   RoR2.Inventory/ItemTransformation
            //      itemTransformation.originalItemIndex = DLC1Content.Items.ExtraLifeVoid.itemIndex;
            // IL_01C9: ldloca.s  V_2
            // IL_01CB: ldsfld    class RoR2.ItemDef RoR2.DLC1Content/Items::ExtraLifeVoid

            int extraLifeItemTransformationVarIndex = -1;
            if (!c.TryGotoNext(MoveType.AfterLabel,
                               x => x.MatchLdloca(out extraLifeItemTransformationVarIndex),
                               x => x.MatchInitobj<Inventory.ItemTransformation>(),
                               x => x.MatchLdloca(extraLifeItemTransformationVarIndex),
                               x => x.MatchLdsfld(typeof(DLC1Content.Items), nameof(DLC1Content.Items.ExtraLifeVoid))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            ILLabel normalReviveLabel = c.DefineLabel();

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<CharacterMaster, bool>>(tryReviveQualityExtraLifeVoid);
            c.Emit(OpCodes.Brfalse, normalReviveLabel);
            c.Emit(OpCodes.Ldc_I4_1);
            c.Emit(OpCodes.Ret);

            c.MarkLabel(normalReviveLabel);

            static bool tryReviveQualityExtraLifeVoid(CharacterMaster master)
            {
                if (master && master.inventory)
                {
                    for (QualityTier qualityTier = QualityTier.Count - 1; qualityTier >= 0; qualityTier--)
                    {
                        ItemIndex qualityExtraLifeVoidItemIndex = ItemQualitiesContent.ItemQualityGroups.ExtraLifeVoid.GetItemIndex(qualityTier);
                        if (qualityExtraLifeVoidItemIndex == ItemIndex.None)
                            continue;

                        ItemIndex qualityExtraLifeVoidConsumedItemIndex = ItemQualitiesContent.ItemQualityGroups.ExtraLifeVoidConsumed.GetItemIndex(qualityTier);
                        if (qualityExtraLifeVoidConsumedItemIndex == ItemIndex.None)
                            qualityExtraLifeVoidConsumedItemIndex = DLC1Content.Items.ExtraLifeVoidConsumed.itemIndex;

                        if (new Inventory.ItemTransformation
                        {
                            originalItemIndex = qualityExtraLifeVoidItemIndex,
                            newItemIndex = qualityExtraLifeVoidConsumedItemIndex,
                            minToTransform = 1,
                            maxToTransform = 1,
                            transformationType = (ItemTransformationTypeIndex)CharacterMasterNotificationQueue.TransformationType.Default
                        }.TryTake(master.inventory, out Inventory.ItemTransformation.TakeResult takeResult))
                        {
                            QualityTier extraLifeVoidQualityTier = qualityTier;

                            CharacterMaster.ExtraLifeServerBehavior extraLifeBehavior = master.gameObject.AddComponent<CharacterMaster.ExtraLifeServerBehavior>();
                            extraLifeBehavior.pendingTransformation = takeResult;
                            extraLifeBehavior.consumedItemIndex = qualityExtraLifeVoidConsumedItemIndex;
                            extraLifeBehavior.completionTime = Run.FixedTimeStamp.now + 2f;
                            extraLifeBehavior.completionCallback += respawnQualityExtraLifeVoid;
                            extraLifeBehavior.soundTime = extraLifeBehavior.completionTime - 1f;
                            extraLifeBehavior.soundCallback += master.PlayExtraLifeVoidSFX;

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
