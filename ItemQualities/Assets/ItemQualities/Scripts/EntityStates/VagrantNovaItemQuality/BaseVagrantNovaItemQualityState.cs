using ItemQualities;
using ItemQualities.Utilities.Extensions;

namespace EntityStates.VagrantNovaItemQuality
{
    public abstract class BaseVagrantNovaItemQualityState : BaseBodyAttachmentState
    {
        protected ItemQualityCounts GetItemCounts()
        {
            return attachedBody && attachedBody.inventory ? attachedBody.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.NovaOnLowHealth) : ItemQualityCounts.zero;
        }
    }
}
