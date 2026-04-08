using RoR2;
using RoR2.Items;

namespace ItemQualities.Items
{
    static class TrueKillOnTimer
    {
        [SystemInitializer]
        static void Init()
        {
            MasterSummon.onServerMasterSummonGlobal += onServerMasterSummonGlobal;
        }

        static void onServerMasterSummonGlobal(MasterSummon.MasterSummonReport summonReport)
        {
            if (!summonReport.masterSummon?.summonerBodyObject || !summonReport.summonMasterInstance)
                return;

            if (!summonReport.masterSummon.summonerBodyObject.TryGetComponent(out CharacterBody summonerBody) || !summonerBody.inventory)
                return;

            int summonerTrueKillTimer = summonerBody.inventory.GetItemCountEffective(ItemQualitiesContent.Items.TrueKillOnTimer);
            if (summonerTrueKillTimer > 0)
            {
                summonReport.summonMasterInstance.inventory.ResetItemPermanent(ItemQualitiesContent.Items.TrueKillOnTimer);
                summonReport.summonMasterInstance.inventory.GiveItemPermanent(ItemQualitiesContent.Items.TrueKillOnTimer, summonerTrueKillTimer);
            }
        }
    }

    public sealed class TrueKillOnTimerBehavior : BaseItemBodyBehavior
    {
        [ItemDefAssociation(useOnServer = true, useOnClient = false)]
        static ItemDef GetItemDef()
        {
            return ItemQualitiesContent.Items.TrueKillOnTimer;
        }

        void OnEnable()
        {
            if (body.master && body.master.TryGetComponent(out DroneRepairMaster droneRepairMaster))
            {
                // Prevent ghost Operator drones from repairing after dying
                // This value should not be restored since that would repair it after this component is removed
                droneRepairMaster.DoNotRepair = true;
                body.master.destroyOnBodyDeath = true;
            }
        }

        void FixedUpdate()
        {
            // This assumes the item was given at spawn
            float timer = body.master ? body.master.currentLifeStopwatch : body.localStartTime.timeSince;
            if (timer >= stack)
            {
                if (body.master)
                {
                    body.master.TrueKill();
                }
                else
                {
                    body.healthComponent.Suicide();
                }
            }
        }
    }
}
