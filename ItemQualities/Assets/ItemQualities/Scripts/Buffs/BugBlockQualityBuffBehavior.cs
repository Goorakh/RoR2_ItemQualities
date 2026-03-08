using RoR2;
using UnityEngine;

namespace ItemQualities.Buffs
{
    public sealed class BugBlockQualityBuffBehavior : QualityBuffBodyBehavior
    {
        [BuffGroupAssociation(QualityBuffBehaviorUsageFlags.Server)]
        static BuffQualityGroup GetBuffGroup()
        {
            return ItemQualitiesContent.BuffQualityGroups.BugBlock;
        }

        GameObject _swarmAttachmentObj;

        void OnDisable()
        {
            setSwarmAttachmentActive(false);
        }

        protected override void OnStacksChanged()
        {
            base.OnStacksChanged();

            setSwarmAttachmentActive(Stacks.TotalQualityCount > 0);
        }

        void setSwarmAttachmentActive(bool shouldBeActive)
        {
            bool hasSwarmActive = _swarmAttachmentObj;
            if (hasSwarmActive != shouldBeActive)
            {
                if (shouldBeActive)
                {
                    _swarmAttachmentObj = Instantiate(ItemQualitiesContent.NetworkedPrefabs.BugSwarmController);
                    _swarmAttachmentObj.GetComponent<NetworkedBodyAttachment>().AttachToGameObjectAndSpawn(gameObject);
                }
                else
                {
                    Destroy(_swarmAttachmentObj);
                    _swarmAttachmentObj = null;
                }
            }
        }
    }
}
