using ItemQualities;
using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using RoR2;

namespace EntityStates.VagrantNovaItemQuality
{
    public sealed class ReadyState : BaseVagrantNovaItemQualityState
    {
        public override void OnEnter()
        {
            base.OnEnter();

            GlobalEventManager.onServerDamageDealt += onServerDamageDealt;
        }

        public override void OnExit()
        {
            GlobalEventManager.onServerDamageDealt -= onServerDamageDealt;

            base.OnExit();
        }

        private void onServerDamageDealt(DamageReport damageReport)
        {
            if (ReferenceEquals(damageReport.victimBody, attachedBody) && damageReport.victimBody.inventory)
            {
                ItemQualityCounts novaOnLowHealth = GetItemCounts();

                float onHitProcChance;
                switch (novaOnLowHealth.HighestQuality)
                {
                    case QualityTier.None:
                        onHitProcChance = 0f;
                        break;
                    case QualityTier.Uncommon:
                        onHitProcChance = 10f;
                        break;
                    case QualityTier.Rare:
                        onHitProcChance = 25f;
                        break;
                    case QualityTier.Epic:
                        onHitProcChance = 40f;
                        break;
                    case QualityTier.Legendary:
                        onHitProcChance = 60f;
                        break;
                    default:
                        Log.Warning($"Quality tier {novaOnLowHealth.HighestQuality} is not implemented");
                        onHitProcChance = 0f;
                        break;
                }

                if (onHitProcChance > 0f && RollUtil.CheckRoll(onHitProcChance, damageReport.victimMaster, false))
                {
                    outer.SetNextState(new ChargingState());
                }
            }
        }
    }
}
