using ItemQualities;
using ItemQualities.ContentManagement;
using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using R2API;
using RoR2;
using RoR2.Audio;
using RoR2BepInExPack.GameAssetPathsBetter;
using System.Collections;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace EntityStates.RoboBallBuddy
{
    public sealed class FireQualityGigaBeam : EntityStates.EngiTurret.EngiTurretWeapon.FireBeam, ISkillState
    {
        private static GameObject _impactEffectPrefab;

        [ContentInitializer]
        private static IEnumerator LoadContent(ContentInitializerArgs args)
        {
            AsyncOperationHandle<GameObject> impactEffectLoad = AddressableUtil.LoadTempAssetAsync<GameObject>(RoR2_DLC3_SolusWing.OverheatBeamImpactEffect_prefab);
            impactEffectLoad.OnSuccess(impactEffectPrefab =>
            {
                _impactEffectPrefab = impactEffectPrefab.InstantiateClone("QualityGigaBeamImpact");
                _impactEffectPrefab.transform.localScale = Vector3.one * 4f;

                args.ContentPack.effectDefs.Add(new EffectDef(_impactEffectPrefab));
            });

            return impactEffectLoad.AsProgressCoroutine(args.ProgressReceiver);
        }

        public static float muzzleDirectionBlendFactor;

        public static float minimumBeamDistanceCoefficient;

        public static LoopSoundDef beamLoopSound;

        public GenericSkill activatorSkillSlot { get; set; }

        private float _duration;
        private float _beamRadius;

        private LoopSoundManager.SoundLoopPtr _beamLoopSoundPtr;

        private Transform _laserMuzzle;

        public override InterruptPriority GetMinimumInterruptPriority()
        {
            return InterruptPriority.PrioritySkill;
        }

        public override void OnEnter()
        {
            hitEffectPrefab = _impactEffectPrefab;

            base.OnEnter();

            ItemQualityCounts roboBallBuddyItem = default;
            if (characterBody && characterBody.inventory)
            {
                roboBallBuddyItem = characterBody.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.RoboBallBuddyItem);
            }

            _beamRadius = roboBallBuddyItem.HighestQuality switch
            {
                QualityTier.Uncommon => 1f,
                QualityTier.Rare => 3f,
                QualityTier.Epic => 8f,
                QualityTier.Legendary => 14f,
                _ => 1f,
            };

            _duration = (roboBallBuddyItem.UncommonCount * 4f) +
                        (roboBallBuddyItem.RareCount * 8f) +
                        (roboBallBuddyItem.EpicCount * 12f) +
                        (roboBallBuddyItem.LegendaryCount * 16f);

            damageCoefficient = (roboBallBuddyItem.UncommonCount * 3f) +
                                (roboBallBuddyItem.RareCount * 6f) +
                                (roboBallBuddyItem.EpicCount * 9f) +
                                (roboBallBuddyItem.LegendaryCount * 12f);

            if (beamLoopSound)
            {
                _beamLoopSoundPtr = LoopSoundManager.PlaySoundLoopLocal(gameObject, beamLoopSound);
            }

            if (isAuthority)
            {
                activatorSkillSlot.SetBlockedCooldownSkillState(true);
            }

            if (laserVfxInstance)
            {
                laserVfxInstance.transform.localScale = Vector3.one * _beamRadius;
            }

            _laserMuzzle = FindModelChild(muzzleString);

            // Fire the initial bullet immediately instead of waiting for the initial tick of damage
            FireBullet(GetLaserRay(), muzzleString, Time.fixedTime);
        }

        public override void OnExit()
        {
            base.OnExit();

            if (isAuthority)
            {
                activatorSkillSlot.SetBlockedCooldownSkillState(false);
            }

            if (_beamLoopSoundPtr.isValid)
            {
                LoopSoundManager.StopSoundLoopLocal(_beamLoopSoundPtr);
                _beamLoopSoundPtr = default;
            }
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();

            StartAimMode();
        }

        public override Ray GetLaserRay()
        {
            Ray ray = base.GetLaserRay();

            if (_laserMuzzle)
            {
                Ray muzzleRay = new Ray(_laserMuzzle.position, _laserMuzzle.forward);

                ray.origin = Vector3.Lerp(ray.origin, muzzleRay.origin, muzzleDirectionBlendFactor);
                ray.direction = Vector3.Slerp(ray.direction, muzzleRay.direction, muzzleDirectionBlendFactor);
            }

            return ray;
        }

        public override bool ShouldFireLaser()
        {
            return fixedAge < _duration;
        }

        public override void ModifyBullet(BulletAttack bulletAttack)
        {
            bulletAttack.radius = _beamRadius;
            bulletAttack.damageType.damageSource = DamageSource.Secondary;

            BulletAttack.HitCallback origHitCallback = bulletAttack.hitCallback;
            bulletAttack.hitCallback = beamHitCallback;

            bool beamHitCallback(BulletAttack bulletAttack, ref BulletAttack.BulletHit hitInfo)
            {
                // Ensure the beam goes at least X distance without stopping so it's always visible and doesn't get stuck on terrain too much when it's larger
                bool canStop = hitInfo.distance >= bulletAttack.radius * minimumBeamDistanceCoefficient;
                bulletAttack.stopperMask = canStop ? LayerIndex.CommonMasks.bullet : 0;

                return origHitCallback(bulletAttack, ref hitInfo);
            }
        }
    }
}
