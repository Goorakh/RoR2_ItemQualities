using RoR2;

namespace ItemQualities.Utilities.Extensions
{
    public static class UniquePickupExtensions
    {
        public static UniquePickup WithQualityTier(this in UniquePickup pickup, QualityTier qualityTier)
        {
            return pickup.isValid ? pickup.WithPickupIndex(QualityCatalog.GetPickupIndexOfQuality(pickup.pickupIndex, qualityTier)) : pickup;
        }
    }
}
