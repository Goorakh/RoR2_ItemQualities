using RoR2;
using System;

namespace ItemQualities
{
    static class DropTableQualityHandler
    {
        static readonly WeightedSelection<QualityTier> _tierSelection = new WeightedSelection<QualityTier>();

        static bool _allowQualityGeneration = true;

        static DropTableQualityHandler()
        {
            _tierSelection.AddChoice(QualityTier.Uncommon, 0.7f);
            _tierSelection.AddChoice(QualityTier.Rare, 0.20f);
            _tierSelection.AddChoice(QualityTier.Epic, 0.08f);
            _tierSelection.AddChoice(QualityTier.Legendary, 0.02f);
        }

        [SystemInitializer]
        static void Init()
        {
            On.RoR2.PickupDropTable.GenerateDrop += PickupDropTable_GenerateDrop;
            On.RoR2.PickupDropTable.GenerateUniqueDrops += PickupDropTable_GenerateUniqueDrops;

            On.RoR2.ShopTerminalBehavior.GenerateNewPickupServer_bool += ShopTerminalBehavior_GenerateNewPickupServer_bool;
        }

        static void ShopTerminalBehavior_GenerateNewPickupServer_bool(On.RoR2.ShopTerminalBehavior.orig_GenerateNewPickupServer_bool orig, ShopTerminalBehavior self, bool newHidden)
        {
            try
            {
                if (self.name.Contains("Duplicator", StringComparison.OrdinalIgnoreCase))
                {
                    _allowQualityGeneration = false;
                }

                orig(self, newHidden);
            }
            finally
            {
                _allowQualityGeneration = true;
            }
        }

        static QualityTier rollQuality(Xoroshiro128Plus rng)
        {
            return _tierSelection.Evaluate(rng.nextNormalizedFloat);
        }

        static PickupIndex tryUpgradeQuality(PickupIndex pickupIndex, Xoroshiro128Plus rng)
        {
            if (!_allowQualityGeneration)
                return pickupIndex;

            if (rng.nextNormalizedFloat > 0.05f)
                return pickupIndex;
            
            QualityTier qualityTier = rollQuality(rng);
            PickupIndex qualityPickupIndex = QualityCatalog.GetPickupIndexOfQuality(pickupIndex, qualityTier);

            if (qualityPickupIndex != PickupIndex.none)
            {
                if (qualityPickupIndex == pickupIndex)
                {
                    Log.Warning($"Pickup {pickupIndex} is missing quality variant {qualityTier}");
                }
                else
                {
                    Log.Debug($"Upgraded tier of {pickupIndex}: {qualityPickupIndex}");
                }

                pickupIndex = qualityPickupIndex;
            }

            return pickupIndex;
        }

        static PickupIndex PickupDropTable_GenerateDrop(On.RoR2.PickupDropTable.orig_GenerateDrop orig, PickupDropTable self, Xoroshiro128Plus rng)
        {
            PickupIndex dropPickupIndex = orig(self, rng);

            dropPickupIndex = tryUpgradeQuality(dropPickupIndex, rng);

            return dropPickupIndex;
        }

        static PickupIndex[] PickupDropTable_GenerateUniqueDrops(On.RoR2.PickupDropTable.orig_GenerateUniqueDrops orig, PickupDropTable self, int maxDrops, Xoroshiro128Plus rng)
        {
            PickupIndex[] dropPickupIncides = orig(self, maxDrops, rng);

            for (int i = 0; i < dropPickupIncides.Length; i++)
            {
                dropPickupIncides[i] = tryUpgradeQuality(dropPickupIncides[i], rng);
            }

            return dropPickupIncides;
        }
    }
}
