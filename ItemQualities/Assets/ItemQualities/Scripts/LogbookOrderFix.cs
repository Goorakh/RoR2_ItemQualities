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

            try
            {
                Entry[] sortedEntries = new Entry[entries.Length];
                Array.Copy(entries, sortedEntries, entries.Length);

                for (int i = 0; i < sortedEntries.Length; i++)
                {
                    if (sortedEntries[i]?.extraData is PickupIndex pickupIndex && QualityCatalog.GetQualityTier(pickupIndex) == QualityTier.None)
                    {
                        for (QualityTier qualityTier = 0; qualityTier < QualityTier.Count; qualityTier++)
                        {
                            int desiredQualityEntryIndex = i + (int)qualityTier + 1;
                            if (desiredQualityEntryIndex >= sortedEntries.Length)
                                break;

                            PickupIndex qualityPickupIndex = QualityCatalog.GetPickupIndexOfQuality(pickupIndex, qualityTier);
                            if (qualityPickupIndex != pickupIndex)
                            {
                                int currentQualityEntryIndex = Array.FindIndex(sortedEntries, e => e?.extraData is PickupIndex pickupIndex && pickupIndex == qualityPickupIndex);

                                if (currentQualityEntryIndex != -1 && currentQualityEntryIndex != desiredQualityEntryIndex)
                                {
                                    Entry qualityEntry = sortedEntries[currentQualityEntryIndex];

                                    if (currentQualityEntryIndex < desiredQualityEntryIndex)
                                    {
                                        Array.Copy(sortedEntries, currentQualityEntryIndex + 1, sortedEntries, currentQualityEntryIndex, desiredQualityEntryIndex - currentQualityEntryIndex);
                                        i--;
                                        desiredQualityEntryIndex--;
                                    }
                                    else // currentIndex > desiredIndex
                                    {
                                        Array.Copy(sortedEntries, desiredQualityEntryIndex, sortedEntries, desiredQualityEntryIndex + 1, currentQualityEntryIndex - desiredQualityEntryIndex);
                                    }

                                    sortedEntries[desiredQualityEntryIndex] = qualityEntry;
                                }
                            }
                        }
                    }
                }

                Array.ConstrainedCopy(sortedEntries, 0, entries, 0, entries.Length);
            }
            catch (Exception e)
            {
                Log.Error_NoCallerPrefix(e);
            }

            return entries;
        }
    }
}
