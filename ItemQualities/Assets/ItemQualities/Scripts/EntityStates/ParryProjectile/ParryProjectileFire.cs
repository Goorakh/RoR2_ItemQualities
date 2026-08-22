using ItemQualities;
using ItemQualities.Utilities.Extensions;
using RoR2;
using RoR2.Projectile;
using UnityEngine.Networking;

namespace EntityStates.ParryProjectile
{
    public sealed class ParryProjectileFire : GenericProjectileBaseState
    {
        private CharacterMasterExtraStatsTracker _masterStats;

        public override void OnEnter()
        {
            base.OnEnter();

            _masterStats = characterBody && characterBody.master ? characterBody.master.GetComponentCached<CharacterMasterExtraStatsTracker>() : null;
            if (!_masterStats)
            {
                if (isAuthority)
                {
                    outer.SetNextStateToMain();
                }

                return;
            }

            projectilePrefab = ProjectileCatalog.GetProjectilePrefab(_masterStats.ParryStoredProjectileInfo.ProjectileIndex);
        }

        public override void ModifyProjectileInfo(ref FireProjectileInfo fireProjectileInfo)
        {
            base.ModifyProjectileInfo(ref fireProjectileInfo);

            ParryStoredProjectileInfo parryStoredProjectileInfo = _masterStats.ParryStoredProjectileInfo;

            float damageCoefficient;
            switch (parryStoredProjectileInfo.QualityTier)
            {
                case QualityTier.Uncommon:
                    damageCoefficient = 1.5f;
                    break;
                case QualityTier.Rare:
                    damageCoefficient = 2.5f;
                    break;
                case QualityTier.Epic:
                    damageCoefficient = 4f;
                    break;
                case QualityTier.Legendary:
                    damageCoefficient = 10f;
                    break;
                default:
                    Log.Error($"Quality tier {parryStoredProjectileInfo.QualityTier} is not implemented");
                    return;
            }

            fireProjectileInfo.damage = parryStoredProjectileInfo.Damage * damageCoefficient;
            fireProjectileInfo.crit |= parryStoredProjectileInfo.Crit;
            fireProjectileInfo.force = parryStoredProjectileInfo.Force;
            fireProjectileInfo.damageTypeOverride = DamageTypeCombo.GenericPrimary;
            fireProjectileInfo.damageColorIndex = DamageColorIndex.Item;
        }

        public override void OnExit()
        {
            base.OnExit();

            if (NetworkServer.active)
            {
                if (_masterStats)
                {
                    _masterStats.ParryStoredProjectileInfo = ParryStoredProjectileInfo.None;
                }
            }
        }
    }
}
