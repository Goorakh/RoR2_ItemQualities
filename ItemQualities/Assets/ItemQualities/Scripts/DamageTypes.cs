using R2API;
using RoR2;

namespace ItemQualities
{
    static class DamageTypes
    {
        public static DamageAPI.ModdedDamageType Frost6s { get; private set; }

        [SystemInitializer]
        static void Init()
        {
            Frost6s = DamageAPI.ReserveDamageType();

            GlobalEventManager.onServerDamageDealt += onServerDamageDealt;
        }

        static void onServerDamageDealt(DamageReport damageReport)
        {
            if (damageReport?.damageInfo == null)
                return;

            if (damageReport.victim && damageReport.victimBody)
            {
                if (damageReport.damageInfo.damageType.HasModdedDamageType(Frost6s))
                {
                    if (!damageReport.victim.isInFrozenState && !damageReport.victimBody.HasBuff(DLC2Content.Buffs.FreezeImmune))
                    {
                        damageReport.victimBody.AddTimedBuff(DLC2Content.Buffs.Frost, 6f, 6);
                    }
                }
            }
        }
    }
}
