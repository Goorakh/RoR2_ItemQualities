using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace ItemQualities.Items
{
    public class SprintOutOfCombatQualityItemBehavior : MonoBehaviour
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

        CharacterBody _body;

        bool _providingBuff;

        void Awake()
        {
            _body = GetComponent<CharacterBody>();
        }

        void OnEnable()
        {
            if (NetworkServer.active)
            {
                _body.onInventoryChanged += onInventoryChanged;
            }
        }

        void OnDisable()
        {
            _body.onInventoryChanged -= onInventoryChanged;

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

        void onInventoryChanged()
        {
            QualityTier buffQualityTier = ItemQualitiesContent.ItemQualityGroups.SprintOutOfCombat.GetItemCountsEffective(_body.inventory).HighestQuality;
            ItemQualitiesContent.BuffQualityGroups.WhipBoost.EnsureBuffQualities(_body, buffQualityTier);
        }

        void setProvidingBuff(bool providingBuff)
        {
            if (providingBuff == _providingBuff)
                return;

            _providingBuff = providingBuff;
            if (providingBuff)
            {
                QualityTier buffQualityTier = ItemQualitiesContent.ItemQualityGroups.SprintOutOfCombat.GetItemCountsEffective(_body.inventory).HighestQuality;
                QualityTierDef buffQualityTierDef = QualityCatalog.GetQualityTierDef(buffQualityTier);

                _body.AddBuff(ItemQualitiesContent.BuffQualityGroups.WhipBoost.GetBuffIndex(buffQualityTier));

                if (_whipActivateEffectIndex != EffectIndex.Invalid)
                {
                    Vector3 bodyForward = _body.transform.forward;
                    if (_body.characterDirection)
                    {
                        if (_body.characterDirection.moveVector.sqrMagnitude > Mathf.Epsilon)
                        {
                            bodyForward = _body.characterDirection.moveVector.normalized;
                        }
                        else
                        {
                            bodyForward = _body.characterDirection.forward;
                        }
                    }

                    EffectData effectData = new EffectData
                    {
                        origin = _body.corePosition,
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
                ItemQualitiesContent.BuffQualityGroups.WhipBoost.EnsureBuffQualities(_body, QualityTier.None);
            }
        }
    }
}
