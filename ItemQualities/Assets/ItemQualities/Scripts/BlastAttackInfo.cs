using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace ItemQualities
{
    internal struct BlastAttackInfo
    {
        public GameObject attacker;

        public GameObject inflictor;

        public TeamIndex teamIndex;

        public AttackerFiltering attackerFiltering;

        public Vector3 position;

        public float radius;

        public BlastAttack.FalloffModel falloffModel;

        public float baseDamage;

        public float baseForce;

        public Vector3 bonusForce;

        public PhysForceFlags physForceFlags;

        public bool crit;

        public DamageTypeCombo damageType;

        public DamageColorIndex damageColorIndex;

        public BlastAttack.LoSType losType;

        public EffectIndex impactEffect;

        public bool canRejectForce;

        public ProcChainMask procChainMask;

        public float procCoefficient;

        public static BlastAttackInfo FromBlastAttack(BlastAttack blastAttack)
        {
            return new BlastAttackInfo
            {
                attacker = blastAttack.attacker,
                inflictor = blastAttack.inflictor,
                teamIndex = blastAttack.teamIndex,
                attackerFiltering = blastAttack.attackerFiltering,
                position = blastAttack.position,
                radius = blastAttack.radius,
                falloffModel = blastAttack.falloffModel,
                baseDamage = blastAttack.baseDamage,
                baseForce = blastAttack.baseForce,
                bonusForce = blastAttack.bonusForce,
                physForceFlags = blastAttack.physForceFlags,
                crit = blastAttack.crit,
                damageType = blastAttack.damageType,
                damageColorIndex = blastAttack.damageColorIndex,
                losType = blastAttack.losType,
                impactEffect = blastAttack.impactEffect,
                canRejectForce = blastAttack.canRejectForce,
                procChainMask = blastAttack.procChainMask,
                procCoefficient = blastAttack.procCoefficient,
            };
        }

        public static void Serialize(NetworkWriter writer, in BlastAttackInfo blastAttackInfo)
        {
            writer.Write(blastAttackInfo.attacker);
            writer.Write(blastAttackInfo.inflictor);
            writer.Write(blastAttackInfo.teamIndex);
            writer.Write((byte)blastAttackInfo.attackerFiltering);
            writer.Write(blastAttackInfo.position);
            writer.Write(blastAttackInfo.radius);
            writer.Write((byte)blastAttackInfo.falloffModel);
            writer.Write(blastAttackInfo.baseDamage);
            writer.Write(blastAttackInfo.baseForce);
            writer.Write(blastAttackInfo.bonusForce);
            writer.WritePhysForceFlags(blastAttackInfo.physForceFlags);
            writer.Write(blastAttackInfo.crit);
            writer.WriteDamageType(blastAttackInfo.damageType);
            writer.Write(blastAttackInfo.damageColorIndex);
            writer.Write((byte)blastAttackInfo.losType);
            writer.WriteEffectIndex(blastAttackInfo.impactEffect);
            writer.Write(blastAttackInfo.canRejectForce);
            writer.Write(blastAttackInfo.procChainMask);
            writer.Write(blastAttackInfo.procCoefficient);
        }

        public static BlastAttackInfo Deserialize(NetworkReader reader)
        {
            return new BlastAttackInfo
            {
                attacker = reader.ReadGameObject(),
                inflictor = reader.ReadGameObject(),
                teamIndex = reader.ReadTeamIndex(),
                attackerFiltering = (AttackerFiltering)reader.ReadByte(),
                position = reader.ReadVector3(),
                radius = reader.ReadSingle(),
                falloffModel = (BlastAttack.FalloffModel)reader.ReadByte(),
                baseDamage = reader.ReadSingle(),
                baseForce = reader.ReadSingle(),
                bonusForce = reader.ReadVector3(),
                physForceFlags = reader.ReadPhysForceFlags(),
                crit = reader.ReadBoolean(),
                damageType = reader.ReadDamageType(),
                damageColorIndex = reader.ReadDamageColorIndex(),
                losType = (BlastAttack.LoSType)reader.ReadByte(),
                impactEffect = reader.ReadEffectIndex(),
                canRejectForce = reader.ReadBoolean(),
                procChainMask = reader.ReadProcChainMask(),
                procCoefficient = reader.ReadSingle(),
            };
        }
    }
}
