using ItemQualities;
using RoR2;
using UnityEngine;

namespace EntityStates.MushroomShield
{
    public sealed class MushroomBubbleDeploy : MushroomBubbleBaseState
    {
        public static string StartSoundString;

        CharacterBody _ownerBody;

        float _startMoveStopwatchValue;

        float _undeployLifetime;

        public override void OnEnter()
        {
            base.OnEnter();

            Util.PlaySound(StartSoundString, gameObject);

            GenericOwnership ownership = GetComponent<GenericOwnership>();
            if (!ownership)
                return;

            if (!ownership.ownerObject)
                return;

            _ownerBody = ownership.ownerObject.GetComponent<CharacterBody>();
            if (!_ownerBody)
                return;

            ItemQualityCounts mushroom = ItemQualitiesContent.ItemQualityGroups.Mushroom.GetItemCountsEffective(_ownerBody.inventory);
            _undeployLifetime = (1 * mushroom.UncommonCount) +
                                (3 * mushroom.RareCount) +
                                (6 * mushroom.EpicCount) +
                                (12 * mushroom.LegendaryCount);

            float scale = 30f;
            switch (mushroom.HighestQuality)
            {
                case QualityTier.Uncommon:
                    scale = 30f;
                    break;
                case QualityTier.Rare:
                    scale = 25f;
                    break;
                case QualityTier.Epic:
                    scale = 20f;
                    break;
                case QualityTier.Legendary:
                    scale = 15f;
                    break;
            }

            EffectRadius = scale;

            transform.localScale = Vector3.one * (scale / 20f);

            _startMoveStopwatchValue = _ownerBody.notMovingStopwatch;
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();

            if (isAuthority)
            {
                if (!_ownerBody || !_ownerBody.healthComponent || !_ownerBody.healthComponent.alive || _ownerBody.notMovingStopwatch < _startMoveStopwatchValue)
                {
                    Undeploy(false);
                }
            }
        }

        public override void Undeploy(bool immediate)
        {
            if (!isAuthority)
                return;
            
            if (immediate)
            {
                outer.SetNextState(new MushroomBubbleFlashOut());
            }
            else
            {
                outer.SetNextState(new MushroomBubbleUndeploy
                {
                    Duration = _undeployLifetime
                });
            }
        }
    }
}
