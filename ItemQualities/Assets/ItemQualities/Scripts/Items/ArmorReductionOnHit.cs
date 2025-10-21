
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using System;




namespace ItemQualities
{
    public class ArmorReductionOnHit
    {
        // Start is called before the first frame update
        [SystemInitializer]
        static void Init()
        {

            IL.RoR2.HealthComponent.TakeDamageProcess += QualityArmorReductionOnHit;

        }

        private static void QualityArmorReductionOnHit(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (!c.TryFindNext(out ILCursor[] foundCursors,
                               x => x.MatchLdsfld(typeof(RoR2Content.Items), nameof(RoR2Content.Items.ArmorReductionOnHit)),
                               x => x.MatchCallOrCallvirt<CharacterBody>(nameof(CharacterBody.ClearTimedBuffs))//,
                               //x => x.MatchLdfld(typeof(RoR2.HealthComponent), nameof(HealthComponent.body))
                               ))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Goto(foundCursors[1].Next, MoveType.After);

            c.Emit(OpCodes.Ldarg_0);
            c.Emit(OpCodes.Ldarg_1);
            c.EmitDelegate<Action<HealthComponent, DamageInfo>>(doExtraDamage);
        }

        static void doExtraDamage(HealthComponent hc, DamageInfo di)
        {
            CharacterBody body = hc.body;
            if (!body) {
                Log.Debug("Body not found");
                return;
            }
            CharacterBody attacker = di.attacker.GetComponent<CharacterBody>();
            if (attacker == null)
            {
                Log.Debug("Attacker not found");
                return;
            }

            body.ClearTimedBuffs(RoR2Content.Buffs.PulverizeBuildup);//just in case

            ItemQualityCounts armorReductionOnHit = ItemQualitiesContent.ItemQualityGroups.ArmorReductionOnHit.GetItemCounts(attacker.inventory);

            

            float damageMultiplyer = 1.0f + (0.20f * armorReductionOnHit.UncommonCount) +
                                            (0.40f * armorReductionOnHit.RareCount) +
                                            (0.60f * armorReductionOnHit.EpicCount) +
                                            (1.00f * armorReductionOnHit.LegendaryCount);

            var blast = new BlastAttack
            {
                attacker = attacker.gameObject,
                inflictor = attacker.gameObject,
                teamIndex = attacker.teamComponent.teamIndex,        
                baseDamage = (attacker.damage) * damageMultiplyer,
                baseForce = 500f,
                position = hc.body.corePosition,                
                radius = 0f,
                falloffModel = BlastAttack.FalloffModel.None,
                crit = attacker.RollCrit(),
                damageType = DamageType.Generic,
                procCoefficient = 1f,
            };
            blast.Fire();
            body.ClearTimedBuffs(RoR2Content.Buffs.PulverizeBuildup);//with proc this stacks again

        }

    }
}
