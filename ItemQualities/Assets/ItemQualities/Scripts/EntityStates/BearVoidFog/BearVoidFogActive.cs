using ItemQualities;
using ItemQualities.Utilities.Extensions;
using RoR2;
using UnityEngine;

namespace EntityStates.BearVoidFog
{
    public sealed class BearVoidFogActive : EntityState
    {
        public static float minimumDuration;

        private float duration;

        private GenericOwnership genericOwnership;
        private bool foundOwner;

        public override void OnEnter()
        {
            base.OnEnter();

            duration = minimumDuration;

            genericOwnership = GetComponent<GenericOwnership>();
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();

            if (!foundOwner && genericOwnership.ownerObject)
            {
                foundOwner = true;
                CharacterBody ownerBody = genericOwnership.ownerObject.GetComponent<CharacterBody>();

                ItemQualityCounts bearVoid = default;
                if (ownerBody && ownerBody.inventory)
                {
                    bearVoid = ownerBody.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.BearVoid);
                }

                float fogDuration = (bearVoid.UncommonCount * 5f) +
                                    (bearVoid.RareCount * 10f) +
                                    (bearVoid.EpicCount * 20f) +
                                    (bearVoid.LegendaryCount * 30f);

                duration = Mathf.Max(duration, fogDuration);
            }

            if (isAuthority && fixedAge > duration)
            {
                outer.SetNextState(new BearVoidFogFadeOut());
            }
        }
    }
}
