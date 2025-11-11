using HG;
using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using RoR2.Items;
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

            IL.RoR2.TeleportOnLowHealthBehavior.Update += ItemHooks.CombineGroupedItemCountsPatch;

            AddressableUtil.LoadAssetAsync<GameObject>(RoR2_DLC2_Items_TeleportOnLowHealth.TeleportOnLowHealthExplosion_prefab).OnSuccess(teleportOnLowHealthExplosion =>
            {
                teleportOnLowHealthExplosion.EnsureComponent<TeleportOnLowHealthAuraQualityController>();
            });
        }

        static void CharacterMaster_TryTeleportOnLowHealthRegen(On.RoR2.CharacterMaster.orig_TryTeleportOnLowHealthRegen orig, CharacterMaster self)
        {
            orig(self);

            for (QualityTier qualityTier = 0; qualityTier < QualityTier.Count; qualityTier++)
            {
                ItemIndex itemIndex = ItemQualitiesContent.ItemQualityGroups.TeleportOnLowHealth.GetItemIndex(qualityTier);
                ItemIndex consumedItemIndex = ItemQualitiesContent.ItemQualityGroups.TeleportOnLowHealthConsumed.GetItemIndex(qualityTier);

                int consumedItemCount = self.inventory.GetItemCount(consumedItemIndex);
                if (consumedItemCount > 0)
                {
                    self.inventory.RemoveItem(consumedItemIndex, consumedItemCount);
                    self.inventory.GiveItem(itemIndex, consumedItemCount);

                    CharacterMasterNotificationQueue.SendTransformNotification(self, consumedItemIndex, itemIndex, CharacterMasterNotificationQueue.TransformationType.TeleportOnLowHealthRegen);
                }
            }
        }

        static void TeleportOnLowHealthBehavior_DestroyTeleportOrb(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (!c.TryFindNext(out ILCursor[] foundCursors,
                               x => x.MatchLdsfld(typeof(DLC2Content.Items), nameof(DLC2Content.Items.TeleportOnLowHealth)),
                               x => x.MatchCallOrCallvirt<Inventory>(nameof(Inventory.RemoveItem)),
                               x => x.MatchCallOrCallvirt<CharacterMasterNotificationQueue>(nameof(CharacterMasterNotificationQueue.SendTransformNotification))))
            {
                Log.Error("Failed to find transmitter consume end instruction");
                return;
            }

            c.Goto(foundCursors[2].Next, MoveType.After);
            ILLabel afterBaseTransmitterConsumeLabel = c.MarkLabel();

            c.Goto(foundCursors[1].Next, MoveType.Before);
            if (!c.TryGotoPrev(MoveType.Before,
                               x => x.MatchLdarg(0),
                               x => x.MatchCallOrCallvirt<BaseItemBodyBehavior>("get_" + nameof(BaseItemBodyBehavior.body)),
                               x => x.MatchCallOrCallvirt<CharacterBody>("get_" + nameof(CharacterBody.inventory))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<TeleportOnLowHealthBehavior, bool>>(tryHandleQualityTransmitters);
            c.Emit(OpCodes.Brtrue, afterBaseTransmitterConsumeLabel);

            static bool tryHandleQualityTransmitters(TeleportOnLowHealthBehavior teleportOnLowHealthBehavior)
            {
                CharacterBody body = teleportOnLowHealthBehavior ? teleportOnLowHealthBehavior.body : null;
                Inventory inventory = body ? body.inventory : null;
                CharacterMaster master = body ? body.master : null;
                if (!inventory)
                    return false;

                bool consumedQualityTransmitter = false;

                ItemQualityCounts teleportOnLowHealth = ItemQualitiesContent.ItemQualityGroups.TeleportOnLowHealth.GetItemCounts(inventory);
                if (teleportOnLowHealth.BaseItemCount == 0 && teleportOnLowHealth.TotalQualityCount > 0)
                {
                    for (QualityTier qualityTier = 0; qualityTier < QualityTier.Count; qualityTier++)
                    {
                        if (teleportOnLowHealth[qualityTier] > 0)
                        {
                            ItemIndex itemIndex = ItemQualitiesContent.ItemQualityGroups.TeleportOnLowHealth.GetItemIndex(qualityTier);
                            ItemIndex consumedItemIndex = ItemQualitiesContent.ItemQualityGroups.TeleportOnLowHealthConsumed.GetItemIndex(qualityTier);

                            inventory.RemoveItem(itemIndex);
                            inventory.GiveItem(consumedItemIndex);

                            if (master)
                            {
                                CharacterMasterNotificationQueue.SendTransformNotification(master, itemIndex, consumedItemIndex, CharacterMasterNotificationQueue.TransformationType.TeleportOnLowHealthRegen);
                            }

                            consumedQualityTransmitter = true;
                            break;
                        }
                    }
                }

                return consumedQualityTransmitter;
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

                    ItemQualityCounts teleportOnLowHealth = ItemQualitiesContent.ItemQualityGroups.TeleportOnLowHealth.GetItemCounts(inventory);

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
            ItemHooks.CombineGroupedItemCountsPatch(il);

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

                    ItemQualityCounts teleportOnLowHealth = ItemQualitiesContent.ItemQualityGroups.TeleportOnLowHealth.GetItemCounts(attackerInventory);

                    bleedDuration += (1.0f * teleportOnLowHealth.UncommonCount) +
                                     (2.0f * teleportOnLowHealth.RareCount) +
                                     (3.5f * teleportOnLowHealth.EpicCount) +
                                     (5.0f * teleportOnLowHealth.LegendaryCount);

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
