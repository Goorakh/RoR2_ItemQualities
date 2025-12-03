using RoR2;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ItemQualities.Items
{
    static class Duplicator
    {
        static ItemIndex[] _allyShareBlacklist = Array.Empty<ItemIndex>();

        [SystemInitializer(typeof(QualityCatalog))]
        static void Init()
        {
            HashSet<ItemIndex> allyShareBlacklist = new HashSet<ItemIndex>();

            addItemGroup(DLC3Content.Items.Duplicator.itemIndex, allyShareBlacklist);

            static void addItemGroup(ItemIndex itemIndex, ICollection<ItemIndex> itemCollection)
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
        }

        public static bool ItemShareFilter(ItemIndex itemIndex)
        {
            if (itemIndex == ItemIndex.None)
                return false;

            ItemDef itemDef = ItemCatalog.GetItemDef(itemIndex);
            if (itemDef.hidden || !itemDef.canRemove || itemDef.ContainsTag(ItemTag.CannotCopy) || itemDef.ContainsTag(ItemTag.OnStageBeginEffect))
                return false;

            if (Array.BinarySearch(_allyShareBlacklist, itemIndex) >= 0)
                return false;

            return true;
        }

        static bool CharacterBody_CheckDroneHasItems(On.RoR2.CharacterBody.orig_CheckDroneHasItems orig, CharacterBody self)
        {
            if (self.IsDrone)
            {
                self.bodyFlags &= ~CharacterBody.BodyFlags.DroneHasItems;
            }

            return orig(self);
        }
    }
}
