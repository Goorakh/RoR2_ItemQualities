using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace ItemQualities.Items
{
    public sealed class GoldOnHurtQualityItemBehavior : MonoBehaviour
    {
        const int BaseMaxMoneyValue = 100;

        CharacterBody _body;

        uint _lastMoneyAmount;

        uint currentMoney => _body && _body.master ? _body.master.money : 0;

        void Awake()
        {
            _body = GetComponent<CharacterBody>();
        }

        void OnEnable()
        {
            if (NetworkServer.active)
            {
                _body.onInventoryChanged += onInventoryChanged;
                updateProvidingBuff();
                _lastMoneyAmount = currentMoney;
            }
        }

        void OnDisable()
        {
            _body.onInventoryChanged -= onInventoryChanged;

            if (NetworkServer.active)
            {
                ItemQualitiesContent.BuffQualityGroups.GoldArmorBuff.EnsureBuffQualities(_body, QualityTier.None);
            }
        }

        void FixedUpdate()
        {
            if (currentMoney != _lastMoneyAmount)
            {
                updateProvidingBuff();
                _lastMoneyAmount = currentMoney;
            }
        }

        void onInventoryChanged()
        {
            QualityTier buffQualityTier = ItemQualitiesContent.ItemQualityGroups.GoldOnHurt.GetItemCountsEffective(_body.inventory).HighestQuality;
            ItemQualitiesContent.BuffQualityGroups.GoldArmorBuff.EnsureBuffQualities(_body, buffQualityTier);
        }

        void updateProvidingBuff()
        {
            int maxMoneyValue = Run.instance.GetDifficultyScaledCost(BaseMaxMoneyValue, Stage.instance.entryDifficultyCoefficient);
            setProvidingBuff(currentMoney <= maxMoneyValue);
        }

        void setProvidingBuff(bool active)
        {
            if (active == ItemQualitiesContent.BuffQualityGroups.GoldArmorBuff.HasBuff(_body))
                return;

            if (active)
            {
                QualityTier buffQualityTier = ItemQualitiesContent.ItemQualityGroups.GoldOnHurt.GetItemCountsEffective(_body.inventory).HighestQuality;
                _body.AddBuff(ItemQualitiesContent.BuffQualityGroups.GoldArmorBuff.GetBuffIndex(buffQualityTier));
            }
            else
            {
                ItemQualitiesContent.BuffQualityGroups.GoldArmorBuff.EnsureBuffQualities(_body, QualityTier.None);
            }
        }
    }
}
