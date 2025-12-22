using RoR2;
using RoR2.ExpansionManagement;
using RoR2.UI.LogBook;
using System;
using System.Collections.Generic;

namespace ItemQualities
{
    static class LogbookOrderFix
    {
        [SystemInitializer]
        static void Init()
        {
            On.RoR2.UI.LogBook.LogBookController.BuildPickupEntries += LogBookController_BuildPickupEntries;
        }

        static Entry[] LogBookController_BuildPickupEntries(On.RoR2.UI.LogBook.LogBookController.orig_BuildPickupEntries orig, Dictionary<ExpansionDef, bool> expansionAvailability)
        {
            Entry[] entries = orig(expansionAvailability);

            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i]?.extraData is PickupIndex pickupIndex && QualityCatalog.GetQualityTier(pickupIndex) == QualityTier.None)
                {
                    for (QualityTier qualityTier = 0; qualityTier < QualityTier.Count; qualityTier++)
                    {
                        PickupIndex qualityPickupIndex = QualityCatalog.GetPickupIndexOfQuality(pickupIndex, qualityTier);
                        if (qualityPickupIndex != pickupIndex)
                        {
                            int currentQualityEntryIndex = Array.FindIndex(entries, e => e?.extraData is PickupIndex pickupIndex && pickupIndex == qualityPickupIndex);
                            int desiredQualityEntryIndex = i + (int)qualityTier + 1;
                            if (currentQualityEntryIndex != desiredQualityEntryIndex)
                            {
                                Entry qualityEntry = entries[currentQualityEntryIndex];

                                if (currentQualityEntryIndex < desiredQualityEntryIndex)
                                {
                                    Array.Copy(entries, currentQualityEntryIndex + 1, entries, currentQualityEntryIndex, desiredQualityEntryIndex - currentQualityEntryIndex);
                                    i--;
                                    desiredQualityEntryIndex--;
                                }
                                else // currentIndex > desiredIndex
                                {
                                    Array.Copy(entries, desiredQualityEntryIndex, entries, desiredQualityEntryIndex + 1, currentQualityEntryIndex -desiredQualityEntryIndex);
                                }

                                entries[desiredQualityEntryIndex] = qualityEntry;
                            }
                        }
                    }

                    i += (int)QualityTier.Count;
                }
            }

            return entries;
        }
    }
}
