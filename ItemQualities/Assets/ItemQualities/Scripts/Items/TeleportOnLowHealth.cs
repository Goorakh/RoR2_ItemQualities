using ItemQualities.Utilities.Extensions;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using RoR2.Projectile;
using UnityEngine;

namespace ItemQualities.Items
{
    internal static class TeleportOnLowHealth
    {
        [SystemInitializer]
        private static void Init()
        {
            On.RoR2.CharacterMaster.TryTeleportOnLowHealthRegen += CharacterMaster_TryTeleportOnLowHealthRegen;

            IL.RoR2.TeleportOnLowHealthBehavior.DestroyTeleportOrb += TeleportOnLowHealthBehavior_DestroyTeleportOrb;

            CharacterBodyExtraStatsTracker.OnSkillActivatedServerGlobal += onSkillActivatedServerGlobal;
        }

        private static void onSkillActivatedServerGlobal(CharacterBodyExtraStatsTracker bodyStats, GenericSkill skill)
        {
            if (!bodyStats.Body || !bodyStats.Body.skillLocator || !ReferenceEquals(bodyStats.Body.skillLocator.secondary, skill))
                return;

            BuffQualityCounts orbCharges = bodyStats.Body.GetBuffCounts(ItemQualitiesContent.BuffQualityGroups.TeleportOnLowHealthOrbCharge);
            if (orbCharges.TotalQualityCount > 0)
            {
                QualityTier qualityTier = orbCharges.HighestQuality;

                Ray aimRay = bodyStats.Body.inputBank.GetAimRay();

                ProjectileManager.instance.FireProjectile(new FireProjectileInfo
                {
                    projectilePrefab = ItemQualitiesContent.ProjectilePrefabs.TeleportOnLowHealthOrbProjectile,
                    position = aimRay.origin,
                    rotation = Util.QuaternionSafeLookRotation(aimRay.direction),
                    owner = bodyStats.gameObject,
                    damage = 0f,
                    crit = bodyStats.Body.RollCrit(),
                    damageColorIndex = DamageColorIndex.Bleed,
                });

                bodyStats.Body.RemoveBuff(ItemQualitiesContent.BuffQualityGroups.TeleportOnLowHealthOrbCharge.GetBuffIndex(qualityTier));
                orbCharges[qualityTier]--;
            }
        }

        private static void CharacterMaster_TryTeleportOnLowHealthRegen(On.RoR2.CharacterMaster.orig_TryTeleportOnLowHealthRegen orig, CharacterMaster self)
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

        private static void TeleportOnLowHealthBehavior_DestroyTeleportOrb(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            VariableDefinition tryTransformResultVar = null;
            if (!c.TryGotoNext(MoveType.After,
                               x => x.MatchLdloca<Inventory.ItemTransformation.TryTransformResult>(il, out tryTransformResultVar),
                               x => x.MatchCallOrCallvirt<Inventory.ItemTransformation>(nameof(Inventory.ItemTransformation.TryTransform))))
            {
                Log.Error("Failed to find transmitter consume transformation call");
                return;
            }

            VariableDefinition itemTransformationVar = null;
            if (!c.Clone().TryGotoPrev(MoveType.Before,
                                       x => x.MatchLdloca(typeof(Inventory.ItemTransformation), il, out itemTransformationVar),
                                       x => x.MatchInitobj<Inventory.ItemTransformation>()))
            {
                Log.Error("Failed to find ItemTransformation variable");
                return;
            }

            c.Emit(OpCodes.Ldarg_0);
            c.Emit(OpCodes.Ldloca, itemTransformationVar);
            c.Emit(OpCodes.Ldloca, tryTransformResultVar);
            c.EmitDelegate<TryConsumeQualityTransmittersDelegate>(tryConsumeQualityTransmitters);

            static bool tryConsumeQualityTransmitters(bool consumedRegularTransmitter, TeleportOnLowHealthBehavior teleportOnLowHealthBehavior, ref Inventory.ItemTransformation itemTransformation, ref Inventory.ItemTransformation.TryTransformResult transformResult)
            {
                if (consumedRegularTransmitter)
                    return true;

                CharacterBody body = teleportOnLowHealthBehavior ? teleportOnLowHealthBehavior.body : null;
                Inventory inventory = body ? body.inventory : null;
                if (inventory)
                {
                    for (QualityTier qualityTier = 0; qualityTier < QualityTier.Count; qualityTier++)
                    {
                        Inventory.ItemTransformation qualityItemTransformation = itemTransformation;
                        qualityItemTransformation.originalItemIndex = QualityCatalog.GetItemIndexOfQuality(itemTransformation.originalItemIndex, qualityTier);
                        qualityItemTransformation.newItemIndex = QualityCatalog.GetItemIndexOfQuality(itemTransformation.newItemIndex, qualityTier);

                        if (qualityItemTransformation.originalItemIndex != itemTransformation.originalItemIndex &&
                            qualityItemTransformation.TryTransform(inventory, out transformResult))
                        {
                            itemTransformation = qualityItemTransformation;
                            return true;
                        }
                    }
                }

                return false;
            }
        }

        private delegate bool TryConsumeQualityTransmittersDelegate(bool consumedRegularTransmitter, TeleportOnLowHealthBehavior teleportOnLowHealthBehavior, ref Inventory.ItemTransformation itemTransformation, ref Inventory.ItemTransformation.TryTransformResult result);
    }
}
