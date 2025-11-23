using HG;
using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using RoR2BepInExPack.GameAssetPathsBetter;
using System;
using UnityEngine;

namespace ItemQualities.Items
{
    static class TeleportOnLowHealth
    {
        [SystemInitializer]
        static void Init()
        {
            On.RoR2.CharacterMaster.TryTeleportOnLowHealthRegen += CharacterMaster_TryTeleportOnLowHealthRegen;

            IL.RoR2.TeleportOnLowHealthBehavior.DestroyTeleportOrb += TeleportOnLowHealthBehavior_DestroyTeleportOrb;

            IL.RoR2.TeleportOnLowHealthBehavior.OnCharacterDeathGlobal += TeleportOnLowHealthBehavior_OnCharacterDeathGlobal;

            IL.RoR2.HealthComponent.DoWarp += HealthComponent_DoWarp;

            AddressableUtil.LoadAssetAsync<GameObject>(RoR2_DLC2_Items_TeleportOnLowHealth.TeleportOnLowHealthExplosion_prefab).OnSuccess(teleportOnLowHealthExplosion =>
            {
                teleportOnLowHealthExplosion.EnsureComponent<TeleportOnLowHealthAuraQualityController>();
            });
        }

        static void CharacterMaster_TryTeleportOnLowHealthRegen(On.RoR2.CharacterMaster.orig_TryTeleportOnLowHealthRegen orig, CharacterMaster self)
        {
            orig(self);

            if (self && self.inventory)
            {
                for (QualityTier qualityTier = 0; qualityTier < QualityTier.Count; qualityTier++)
                {
                    ItemIndex itemIndex = ItemQualitiesContent.ItemQualityGroups.TeleportOnLowHealth.GetItemIndex(qualityTier);
                    ItemIndex consumedItemIndex = ItemQualitiesContent.ItemQualityGroups.TeleportOnLowHealthConsumed.GetItemIndex(qualityTier);

                    if (itemIndex != ItemIndex.None && consumedItemIndex != ItemIndex.None)
                    {
                        Inventory.ItemTransformation regenerateTransformation = new Inventory.ItemTransformation
                        {
                            originalItemIndex = consumedItemIndex,
                            newItemIndex = itemIndex,
                            maxToTransform = int.MaxValue,
                            transformationType = (ItemTransformationTypeIndex)CharacterMasterNotificationQueue.TransformationType.TeleportOnLowHealthRegen
                        };

                        regenerateTransformation.TryTransform(self.inventory, out _);
                    }
                }
            }
        }

        static void TeleportOnLowHealthBehavior_DestroyTeleportOrb(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (!c.TryGotoNext(MoveType.After,
                               x => x.MatchCallOrCallvirt<Inventory.ItemTransformation>(nameof(Inventory.ItemTransformation.TryTransform))))
            {
                Log.Fatal("Failed to find transmitter consume transformation call");
                return;
            }

            int itemTransformationLocalIndex = -1;
            if (!c.TryFindPrev(out _,
                               x => x.MatchLdloca(typeof(Inventory.ItemTransformation), il, out itemTransformationLocalIndex),
                               x => x.MatchInitobj<Inventory.ItemTransformation>()))
            {
                Log.Fatal("Failed to find ItemTransformation variable");
                return;
            }

            c.Emit(OpCodes.Ldarg_0);
            c.Emit(OpCodes.Ldloc, itemTransformationLocalIndex);
            c.EmitDelegate<Func<bool, TeleportOnLowHealthBehavior, Inventory.ItemTransformation, bool>>(tryConsumeQualityTransmitters);

            static bool tryConsumeQualityTransmitters(bool consumedRegularTransmitter, TeleportOnLowHealthBehavior teleportOnLowHealthBehavior, Inventory.ItemTransformation itemTransformation)
            {
                if (consumedRegularTransmitter)
                    return true;

                CharacterBody body = teleportOnLowHealthBehavior ? teleportOnLowHealthBehavior.body : null;
                Inventory inventory = body ? body.inventory : null;
                CharacterMaster master = body ? body.master : null;
                if (inventory)
                {
                    for (QualityTier qualityTier = 0; qualityTier < QualityTier.Count; qualityTier++)
                    {
                        ItemIndex itemIndex = ItemQualitiesContent.ItemQualityGroups.TeleportOnLowHealth.GetItemIndex(qualityTier);
                        ItemIndex consumedItemIndex = ItemQualitiesContent.ItemQualityGroups.TeleportOnLowHealthConsumed.GetItemIndex(qualityTier);

                        Inventory.ItemTransformation qualityItemTransformation = itemTransformation;
                        qualityItemTransformation.originalItemIndex = QualityCatalog.GetItemIndexOfQuality(itemTransformation.originalItemIndex, qualityTier);
                        qualityItemTransformation.newItemIndex = QualityCatalog.GetItemIndexOfQuality(itemTransformation.newItemIndex, qualityTier);

                        if (qualityItemTransformation.TryTransform(inventory, out Inventory.ItemTransformation.TryTransformResult transformResult))
                        {
                            return true;
                        }
                    }
                }

                return false;
            }
        }

        static void TeleportOnLowHealthBehavior_OnCharacterDeathGlobal(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (c.TryGotoNext(MoveType.Before,
                              x => x.MatchCallOrCallvirt<CharacterBody>(nameof(CharacterBody.ExtendTimedBuffIfPresent))) &&
                c.TryGotoPrev(MoveType.After,
                              x => x.MatchLdcR4(1f)))
            {
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate<Func<float, TeleportOnLowHealthBehavior, float>>(getBuffExtensionDuration);

                static float getBuffExtensionDuration(float extensionDuration, TeleportOnLowHealthBehavior teleportOnLowHealthBehavior)
                {
                    CharacterBody body = teleportOnLowHealthBehavior ? teleportOnLowHealthBehavior.body : null;
                    Inventory inventory = body ? body.inventory : null;

                    ItemQualityCounts teleportOnLowHealth = ItemQualitiesContent.ItemQualityGroups.TeleportOnLowHealth.GetItemCountsEffective(inventory);

                    if (teleportOnLowHealth.UncommonCount > 0)
                    {
                        extensionDuration += 0.5f;
                    }

                    if (teleportOnLowHealth.RareCount > 0)
                    {
                        extensionDuration += 1.5f;
                    }

                    if (teleportOnLowHealth.EpicCount > 0)
                    {
                        extensionDuration += 3f;
                    }

                    if (teleportOnLowHealth.LegendaryCount > 0)
                    {
                        extensionDuration += 5f;
                    }

                    return extensionDuration;
                }
            }
            else
            {
                Log.Error("Failed to find patch location");
            }
        }

        static void HealthComponent_DoWarp(ILContext il)
        {
            if (!il.Method.TryFindParameter<CharacterBody>("attackerBody", out ParameterDefinition attackerBodyParameter))
            {
                Log.Error("Failed to find attackerBody parameter");
                return;
            }

            ILCursor c = new ILCursor(il);
            
            if (c.TryGotoNext(MoveType.Before,
                              x => x.MatchCallOrCallvirt<DotController>(nameof(DotController.InflictDot))) &&
                c.TryGotoPrev(MoveType.After,
                              x => x.MatchLdcR4(5f)))
            {
                c.Emit(OpCodes.Ldarg, attackerBodyParameter);
                c.EmitDelegate<Func<float, CharacterBody, float>>(getBleedDuration);

                static float getBleedDuration(float bleedDuration, CharacterBody attackerBody)
                {
                    Inventory attackerInventory = attackerBody ? attackerBody.inventory : null;

                    ItemQualityCounts teleportOnLowHealth = ItemQualitiesContent.ItemQualityGroups.TeleportOnLowHealth.GetItemCountsEffective(attackerInventory);

                    if (teleportOnLowHealth.TotalQualityCount > 0)
                    {
                        bleedDuration += (1.0f * teleportOnLowHealth.UncommonCount) +
                                         (2.0f * teleportOnLowHealth.RareCount) +
                                         (3.5f * teleportOnLowHealth.EpicCount) +
                                         (5.0f * teleportOnLowHealth.LegendaryCount);
                    }

                    return bleedDuration;
                }
            }
            else
            {
                Log.Error("Failed to find patch location");
            }
        }
    }
}
