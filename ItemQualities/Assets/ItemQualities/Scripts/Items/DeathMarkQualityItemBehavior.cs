using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using RoR2;
using RoR2BepInExPack.GameAssetPathsBetter;
using UnityEngine;

namespace ItemQualities.Items
{
    public class DeathMarkQualityItemBehavior : QualityItemBodyBehavior
    {
        [ItemGroupAssociation(QualityItemBehaviorUsageFlags.Server | QualityItemBehaviorUsageFlags.Client)]
        private static ItemQualityGroup GetItemGroup() => ItemQualitiesContent.ItemQualityGroups.DeathMark;

        private static AK.Wwise.Bank _railgunnerBank;

        private static int instanceCount;

        [SystemInitializer]
        private static void Init()
        {
            AddressableUtil.LoadAssetAsync<WwiseBankReference>(Wwise._12364C84_F36A_4AB9_8588_1A2AF5386BE0_asset).OnSuccess(railgunnerBankReference =>
            {
                _railgunnerBank = new AK.Wwise.Bank
                {
                    WwiseObjectReference = railgunnerBankReference
                };
            });
        }

        private void OnEnable()
        {
            _railgunnerBank?.Load();

            if (++instanceCount == 1)
            {
                GlobalEventManager.onServerCharacterExecuted += OnServerCharacterExecuted;
                MasterSummon.onServerMasterSummonGlobal += OnServerMasterSummonGlobal;
            }
        }

        private void OnDisable()
        {
            _railgunnerBank?.Unload();

            if (--instanceCount == 0)
            {
                GlobalEventManager.onServerCharacterExecuted -= OnServerCharacterExecuted;
                MasterSummon.onServerMasterSummonGlobal -= OnServerMasterSummonGlobal;
            }
        }

        private static void OnServerMasterSummonGlobal(MasterSummon.MasterSummonReport report)
        {
            if (!report.leaderBodyInstance || !report.leaderBodyInstance.inventory)
            {
                return;
            }

            if (!report.summonBodyInstance || !report.summonBodyInstance.IsGhost())
            {
                return;
            }

            ItemQualityCounts deathMark = report.leaderBodyInstance.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.DeathMark);

            if (deathMark.TotalQualityCount > 0)
            {
                report.leaderBodyInstance.AddUnmanagedBuff(ItemQualitiesContent.Buffs.DeathMarkSouls);
            }
        }

        private static void OnServerCharacterExecuted(DamageReport report, float damage)
        {
            if (report == null || !report.attackerBody || !report.attackerBody.inventory || !report.victimBody)
                return;

            ItemQualityCounts deathMark = report.attackerBody.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.DeathMark);
            if (deathMark.TotalQualityCount == 0)
                return;

            const int maxBuffs = 100;
            int maxBuffsToAdd = maxBuffs - report.attackerBody.GetBuffCount(ItemQualitiesContent.Buffs.DeathMarkSouls);

            float executeFraction = damage / report.victim.fullCombinedHealth;
            float executeHealthPercent = executeFraction * 100f;
            int desiredBuffsToAdd = (int)(executeHealthPercent * 0.5f);

            int buffsToAdd = Mathf.Min(desiredBuffsToAdd, maxBuffsToAdd);
            for (int i = 0; i < buffsToAdd; i++)
            {
                report.attackerBody.AddUnmanagedBuff(ItemQualitiesContent.Buffs.DeathMarkSouls);
            }
        }
    }
}
