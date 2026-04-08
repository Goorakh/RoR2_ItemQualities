using R2API;
using RoR2;
using RoR2.Items;
using UnityEngine;

namespace ItemQualities
{
    static class DamageTypes
    {
        public static DamageAPI.ModdedDamageType Frost6s { get; private set; }

        public static DamageAPI.ModdedDamageType ForceAddToSharedSuffering { get; private set; }

        [SystemInitializer]
        static void Init()
        {
            Frost6s = DamageAPI.ReserveDamageType();
            ForceAddToSharedSuffering = DamageAPI.ReserveDamageType();

            GlobalEventManager.onServerDamageDealt += onServerDamageDealt;
        }

        static void onServerDamageDealt(DamageReport damageReport)
        {
            if (damageReport?.damageInfo == null)
                return;

            DamageInfo damageInfo = damageReport.damageInfo;

            GameObject attacker = damageReport.attacker;

            CharacterBody victimBody = damageReport.victimBody;
            HealthComponent victimHealthComponent = damageReport.victim;

            if (victimHealthComponent && victimBody)
            {
                if (damageInfo.damageType.HasModdedDamageType(Frost6s))
                {
                    if (!victimHealthComponent.isInFrozenState && !victimBody.HasBuff(DLC2Content.Buffs.FreezeImmune))
                    {
                        victimBody.AddTimedBuff(DLC2Content.Buffs.Frost, 6f, 6);
                    }
                }

                if (damageInfo.damageType.HasModdedDamageType(ForceAddToSharedSuffering))
                {
                    if (victimBody.teamComponent.teamIndex != TeamIndex.None && !victimBody.HasBuff(DLC3Content.Buffs.SharedSuffering))
                    {
                        if (attacker && attacker.TryGetComponent(out SharedSufferingItemBehaviour sharedSufferingItemBehaviour))
                        {
                            victimBody.AddBuff(DLC3Content.Buffs.SharedSuffering);
                            if (!sharedSufferingItemBehaviour.afflicted.Contains(victimBody))
                            {
                                sharedSufferingItemBehaviour.afflicted.Add(victimBody);
                                sharedSufferingItemBehaviour.afflictedDirty = true;
                            }
                        }
                    }
                }
            }
        }
    }
}
