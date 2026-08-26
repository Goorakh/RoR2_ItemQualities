using ItemQualities;
using ItemQualities.Utilities.Extensions;
using RoR2;
using UnityEngine;

namespace EntityStates.VagrantNovaItemQualityStandalone
{
    public abstract class BaseVagrantNovaItemQualityStandaloneState : EntityState
    {
        protected NovaOnLowHealthDelayBlast delayBlast { get; private set; }

        protected CharacterBody attachedBody { get; private set; }

        protected ItemQualityCounts GetItemCounts()
        {
            return attachedBody && attachedBody.inventory ? attachedBody.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.NovaOnLowHealth) : ItemQualityCounts.zero;
        }

        public override void OnEnter()
        {
            base.OnEnter();

            delayBlast = GetComponent<NovaOnLowHealthDelayBlast>();

            GameObject owner = GetComponent<GenericOwnership>().ownerObject;
            attachedBody = owner ? owner.GetComponent<CharacterBody>() : null;
        }
    }
}
