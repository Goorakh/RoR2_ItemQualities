using HG;
using ItemQualities.Utilities.Extensions;
using R2API.Utils;
using RoR2;
using System.Collections.Generic;

namespace ItemQualities
{
    static class QualityPickupTransmutationPatcher
    {
        [InitDuringStartupPhase(GameInitPhase.PreFrame)]
        static void Init()
        {
            SystemInitializerInjector.InjectDependency(typeof(PickupTransmutationManager), typeof(QualityCatalog));

            On.RoR2.PickupTransmutationManager.RebuildPickupGroups += PickupTransmutationManager_RebuildPickupGroups;

            On.RoR2.Util.RollTemporaryItemFromItemIndex += Util_RollTemporaryItemFromItemIndex;
        }

        static void PickupTransmutationManager_RebuildPickupGroups(On.RoR2.PickupTransmutationManager.orig_RebuildPickupGroups orig)
        {
            orig();

            List<PickupIndex[]> newPickupGroups = new List<PickupIndex[]>();

            List<PickupIndex> pickupsBuffer = new List<PickupIndex>();

            foreach (PickupIndex[] pickupGroup in PickupTransmutationManager.pickupGroups)
            {
                pickupsBuffer.EnsureCapacity(pickupGroup.Length);

                for (QualityTier qualityTier = 0; qualityTier < QualityTier.Count; qualityTier++)
                {
                    foreach (PickupIndex pickupIndex in pickupGroup)
                    {
                        PickupIndex qualityPickupIndex = QualityCatalog.GetPickupIndexOfQuality(pickupIndex, qualityTier);
                        if (qualityPickupIndex.isValid && qualityPickupIndex != pickupIndex)
                        {
                            PickupIndex[] currentGroup = ArrayUtils.GetSafe(PickupTransmutationManager.pickupGroupMap, qualityPickupIndex.value);
                            if (currentGroup == null || currentGroup.Length == 0)
                            {
                                pickupsBuffer.Add(qualityPickupIndex);
                            }
                            else
                            {
                                Log.Debug($"Skipping pickup {qualityPickupIndex} because it is already mapped to a pickup group: [{string.Join(", ", currentGroup)}]");
                            }
                        }
                    }

                    if (pickupsBuffer.Count > 0)
                    {
                        newPickupGroups.Add(pickupsBuffer.ToArray());

                        pickupsBuffer.Clear();
                    }
                }
            }

            if (newPickupGroups.Count > 0)
            {
                PickupTransmutationManager.pickupGroups = ArrayUtils.Join(PickupTransmutationManager.pickupGroups, newPickupGroups.ToArray());

                foreach (PickupIndex[] newPickupGroup in newPickupGroups)
                {
                    foreach (PickupIndex pickup in newPickupGroup)
                    {
                        if (ArrayUtils.IsInBounds(PickupTransmutationManager.pickupGroupMap, pickup.value))
                        {
                            PickupTransmutationManager.pickupGroupMap[pickup.value] = newPickupGroup;
                        }
                    }
                }
            }

            Log.Debug($"Added {newPickupGroups.Count} quality pickup group(s)");
        }

        static ItemIndex Util_RollTemporaryItemFromItemIndex(On.RoR2.Util.orig_RollTemporaryItemFromItemIndex orig, ItemIndex itemIndex)
        {
            return orig(QualityCatalog.GetItemIndexOfQuality(itemIndex, QualityTier.None));
        }
    }
}
