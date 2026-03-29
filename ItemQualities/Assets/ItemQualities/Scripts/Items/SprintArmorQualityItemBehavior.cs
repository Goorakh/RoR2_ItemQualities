using RoR2;
using UnityEngine;

namespace ItemQualities.Items
{
    public sealed class SprintArmorQualityItemBehavior : QualityItemBodyBehavior
    {
        [ItemGroupAssociation(QualityItemBehaviorUsageFlags.Server)]
        static ItemQualityGroup GetItemGroup()
        {
            return ItemQualitiesContent.ItemQualityGroups.SprintArmor;
        }

        float _activationWindow;
        bool _heldForward;

        void FixedUpdate()
        {
            Vector3 moveVector = Body.inputBank.moveVector;
            Vector3 aimVector = Body.inputBank.aimDirection;
            aimVector.y = 0;
            float angleDiff = Vector3.Angle(moveVector.normalized, aimVector);

            if (!Body.HasBuff(ItemQualitiesContent.Buffs.SprintArmorDashCooldown) &&
            angleDiff < 70 && moveVector.magnitude > 0.2)
            {
                if (!_heldForward)
                {
                    _heldForward = true;
                    if (_activationWindow > 0)
                    {
                        addDashAttachment();
                    }
                    else
                    {
                        _activationWindow = 0.2f;
                    }
                }
            }
            else
            {
                _heldForward = false;
            }
            _activationWindow -= Time.deltaTime;
        }

        void addDashAttachment()
        {
            GameObject sprintArmorDashObj = GameObject.Instantiate(ItemQualitiesContent.NetworkedPrefabs.SprintArmorDashAttachment);
            NetworkedBodyAttachment sprintArmorDashAttachment = sprintArmorDashObj.GetComponent<NetworkedBodyAttachment>();
            sprintArmorDashAttachment.AttachToGameObjectAndSpawn(Body.gameObject);
        }
    }
}
