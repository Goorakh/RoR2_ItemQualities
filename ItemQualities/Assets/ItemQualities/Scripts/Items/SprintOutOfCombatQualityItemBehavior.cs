using ItemQualities.Utilities.Extensions;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace ItemQualities.Items
{
    public sealed class SprintOutOfCombatQualityItemBehavior : QualityItemBodyBehavior
    {
        static EffectIndex _whipActivateEffectIndex = EffectIndex.Invalid;

        [SystemInitializer(typeof(EffectCatalogUtils))]
        static void Init()
        {
            _whipActivateEffectIndex = EffectCatalogUtils.FindEffectIndex("SprintActivate");
            if (_whipActivateEffectIndex == EffectIndex.Invalid)
            {
                Log.Warning("Failed to find whip activate effect index");
            }
        }

        [ItemGroupAssociation(QualityItemBehaviorUsageFlags.Server)]
        static ItemQualityGroup GetItemGroup()
        {
            return ItemQualitiesContent.ItemQualityGroups.SprintOutOfCombat;
        }

        bool _providingBuff;

        void OnDisable()
        {
            if (NetworkServer.active)
            {
                setProvidingBuff(false);
            }
        }

        void FixedUpdate()
        {
            if (NetworkServer.active)
            {
                setProvidingBuff((TeleporterInteraction.instance && TeleporterInteraction.instance.isCharged) || (MoonBatteryMissionController.instance && MoonBatteryMissionController.instance.numChargedBatteries >= MoonBatteryMissionController.instance.numRequiredBatteries));
            }
        }

        protected override void OnStacksChanged()
        {
            base.OnStacksChanged();

            Body.ConvertQualityBuffsToTier(ItemQualitiesContent.BuffQualityGroups.WhipBoost, Stacks.HighestQuality);
        }

        void setProvidingBuff(bool providingBuff)
        {
            if (providingBuff == _providingBuff)
                return;

            _providingBuff = providingBuff;
            if (providingBuff)
            {
                QualityTier buffQualityTier = Stacks.HighestQuality;
                QualityTierDef buffQualityTierDef = QualityCatalog.GetQualityTierDef(buffQualityTier);

                Body.AddBuff(ItemQualitiesContent.BuffQualityGroups.WhipBoost.GetBuffIndex(buffQualityTier));

                if (_whipActivateEffectIndex != EffectIndex.Invalid)
                {
                    Vector3 bodyForward = Body.transform.forward;
                    if (Body.characterDirection)
                    {
                        if (Body.characterDirection.moveVector.sqrMagnitude > Mathf.Epsilon)
                        {
                            bodyForward = Body.characterDirection.moveVector.normalized;
                        }
                        else
                        {
                            bodyForward = Body.characterDirection.forward;
                        }
                    }

                    EffectData effectData = new EffectData
                    {
                        origin = Body.corePosition,
                        rotation = Util.QuaternionSafeLookRotation(bodyForward)
                    };

                    if (buffQualityTierDef)
                    {
                        effectData.color = buffQualityTierDef.color;
                    }

                    EffectManager.SpawnEffect(_whipActivateEffectIndex, effectData, true);
                }
            }
            else
            {
                Body.RemoveAllQualityBuffs(ItemQualitiesContent.BuffQualityGroups.WhipBoost);
            }
        }
    }
}
