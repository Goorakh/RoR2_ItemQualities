using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using RoR2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ItemQualities.Items
{
    internal static class Duplicator
    {
        private static ItemIndex[] _allyShareBlacklist = Array.Empty<ItemIndex>();

        [SystemInitializer(typeof(QualityCatalog))]
        private static void Init()
        {
            HashSet<ItemIndex> allyShareBlacklist = new HashSet<ItemIndex>((int)QualityTier.Count + 1);

            addItemGroup(DLC3Content.Items.Duplicator.itemIndex, allyShareBlacklist);

            static void addItemGroup(ItemIndex itemIndex, HashSet<ItemIndex> itemCollection)
            {
                if (itemIndex == ItemIndex.None)
                    return;

                ItemQualityGroup itemGroup = QualityCatalog.GetItemQualityGroup(QualityCatalog.FindItemQualityGroupIndex(itemIndex));
                if (itemGroup)
                {
                    for (QualityTier qualityTier = QualityTier.None; qualityTier < QualityTier.Count; qualityTier++)
                    {
                        ItemIndex qualityItemIndex = itemGroup.GetItemIndex(qualityTier);
                        if (qualityItemIndex != ItemIndex.None)
                        {
                            itemCollection.Add(qualityItemIndex);
                        }
                    }
                }
                else
                {
                    itemCollection.Add(itemIndex);
                }
            }

            _allyShareBlacklist = allyShareBlacklist.ToArray();
            Array.Sort(_allyShareBlacklist);

            On.RoR2.CharacterBody.CheckDroneHasItems += CharacterBody_CheckDroneHasItems;

            MethodInfo handleDuplicatorMethod = typeof(ItemDef).GetMethods(ReflectionUtil.AllFlags).SingleOrDefault(m => m.Name.StartsWith("<AttemptGrant>g__HandleDuplicator|"));
            if (handleDuplicatorMethod != null)
            {
                new ILHook(handleDuplicatorMethod, ItemDef_AttemptGrant_HandleDuplicator);
            }
            else
            {
                Log.Error("Failed to find ItemDef.AttemptGrant HandleDuplicator method");
            }
        }

        public static bool ItemShareFilter(ItemIndex itemIndex)
        {
            if (itemIndex == ItemIndex.None)
                return false;

            ItemDef itemDef = ItemCatalog.GetItemDef(itemIndex);
            if (itemDef.hidden || !itemDef.canRemove)
                return false;

            if (itemDef.ContainsTag(ItemTag.CannotCopy) ||
                itemDef.ContainsTag(ItemTag.OnStageBeginEffect) ||
                itemDef.ContainsTag(ItemTag.Scrap))
            {
                return false;
            }

            if (Array.BinarySearch(_allyShareBlacklist, itemIndex) >= 0)
                return false;

            return true;
        }

        public static ItemIndex GetItemToShare(ItemIndex itemIndex)
        {
            ItemIndex sharedItemIndex = itemIndex;
            bool canShareItem = ItemShareFilter(sharedItemIndex);

            // For quality items that cannot be shared: Find highest quality tier that can
            if (!canShareItem)
            {
                ItemQualityGroupIndex sharedItemGroupIndex = QualityCatalog.FindItemQualityGroupIndex(sharedItemIndex);
                if (sharedItemGroupIndex != ItemQualityGroupIndex.Invalid)
                {
                    ItemQualityGroup sharedItemGroup = QualityCatalog.GetItemQualityGroup(sharedItemGroupIndex);
                    for (QualityTier qualityTier = QualityCatalog.GetQualityTier(sharedItemIndex) - 1; qualityTier >= QualityTier.None; qualityTier--)
                    {
                        ItemIndex potentialItemIndex = sharedItemGroup.GetItemIndex(qualityTier);
                        if (ItemShareFilter(potentialItemIndex))
                        {
                            sharedItemIndex = potentialItemIndex;
                            canShareItem = true;
                            break;
                        }
                    }
                }
            }

            return canShareItem ? sharedItemIndex : ItemIndex.None;
        }

        private static bool CharacterBody_CheckDroneHasItems(On.RoR2.CharacterBody.orig_CheckDroneHasItems orig, CharacterBody self)
        {
            if (self.IsDrone)
            {
                self.bodyFlags &= ~CharacterBody.BodyFlags.DroneHasItems;
            }

            return orig(self);
        }

        private static void ItemDef_AttemptGrant_HandleDuplicator(ILContext il)
        {
            if (!il.Method.TryFindParameter<CharacterBody>(out ParameterDefinition pickupBodyParameter))
            {
                Log.Error("Failed to find pickupBody parameter");
                return;
            }

            if (!il.Method.TryFindParameter<float>("countToAdd", out ParameterDefinition countToAddParameter))
            {
                Log.Error("Failed to find countToAdd parameter");
                return;
            }

            ILCursor c = new ILCursor(il);

            c.Emit(OpCodes.Ldarg, pickupBodyParameter);
            c.Emit(OpCodes.Ldarg, countToAddParameter);
            c.EmitDelegate<Func<CharacterBody, float, float>>(getCountToAdd);
            c.Emit(OpCodes.Starg, countToAddParameter);

            static float getCountToAdd(CharacterBody body, float countToAdd)
            {
                if (body && body.inventory)
                {
                    ItemQualityCounts duplicator = body.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.Duplicator);
                    if (duplicator.TotalQualityCount > 0)
                    {
                        // +1 temp item per quality
                        countToAdd += (int)(duplicator.HighestQuality + 1);
                    }
                }

                return countToAdd;
            }
        }
    }
}
