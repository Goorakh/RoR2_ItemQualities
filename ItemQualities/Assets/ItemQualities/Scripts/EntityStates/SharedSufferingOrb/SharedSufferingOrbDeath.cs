using ItemQualities;
using ItemQualities.ContentManagement;
using ItemQualities.Items;
using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using R2API;
using RoR2;
using RoR2BepInExPack.GameAssetPaths.Version_1_35_0;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace EntityStates.SharedSufferingOrb
{
    public sealed class SharedSufferingOrbDeath : EntityState
    {
        static GameObject _deathEffectPrefab;

        [ContentInitializer]
        static IEnumerator LoadContent(ContentIntializerArgs args)
        {
            AsyncOperationHandle<GameObject> timeCrystalDeathLoad = AddressableUtil.LoadTempAssetAsync<GameObject>(RoR2_Base_WeeklyRun.TimeCrystalDeath_prefab);
            timeCrystalDeathLoad.OnSuccess(timeCrystalDeath =>
            {
                _deathEffectPrefab = timeCrystalDeath.InstantiateClone("SharedSufferingOrbDeathEffect", false);

                args.ContentPack.effectDefs.Add(new EffectDef(_deathEffectPrefab));
            });

            return timeCrystalDeathLoad.AsProgressCoroutine(args.ProgressReceiver);
        }

        public static float ExplosionDamageCoefficient;

        public static float ExplosionForce;

        public override void OnEnter()
        {
            base.OnEnter();
            explode();
        }

        void explode()
        {
            if (modelLocator)
            {
                if (modelLocator.modelBaseTransform)
                {
                    Destroy(modelLocator.modelBaseTransform.gameObject);
                }

                if (modelLocator.modelTransform)
                {
                    Destroy(modelLocator.modelTransform.gameObject);
                }
            }

            SharedSufferingOrbController orbController = GetComponent<SharedSufferingOrbController>();
            float explosionRadius = orbController ? orbController.BlastRadius : 0f;

            if (_deathEffectPrefab)
            {
                EffectManager.SpawnEffect(_deathEffectPrefab, new EffectData
                {
                    origin = transform.position,
                    scale = explosionRadius,
                    rotation = Quaternion.identity
                }, false);
            }

            if (NetworkServer.active)
            {
                GenericOwnership ownership = GetComponent<GenericOwnership>();

                GameObject owner = ownership ? ownership.ownerObject : null;
                CharacterBody ownerBody = owner ? owner.GetComponent<CharacterBody>() : null;
                TeamIndex ownerTeamIndex = ownerBody ? ownerBody.teamComponent.teamIndex : TeamIndex.None;

                Vector3 blastPosition = transform.position;
                float blastDamage = ExplosionDamageCoefficient * (ownerBody ? ownerBody.damage : Run.instance.teamlessDamageCoefficient);

                foreach (CharacterBody body in CharacterBody.readOnlyInstancesList)
                {
                    if (!FriendlyFireManager.ShouldSplashHitProceed(body.healthComponent, ownerTeamIndex))
                        continue;

                    if (body == ownerBody || body == characterBody)
                        continue;

                    if (Vector3.Distance(body.corePosition, blastPosition) >= explosionRadius)
                        continue;

                    DamageInfo damageInfo = new DamageInfo
                    {
                        attacker = owner,
                        inflictor = gameObject,
                        damage = blastDamage,
                        crit = ownerBody && ownerBody.RollCrit(),
                        damageType = DamageType.AOE,
                        damageColorIndex = DamageColorIndex.Electrocution,
                        procCoefficient = 0f,
                        position = blastPosition,
                        force = (ExplosionForce * (body.corePosition - blastPosition).normalized) + ((ExplosionForce * 0.5f) * Vector3.up)
                    };

                    damageInfo.damageType.AddModdedDamageType(DamageTypes.ForceAddToSharedSuffering);

                    body.healthComponent.TakeDamage(damageInfo);
                    GlobalEventManager.instance.OnHitEnemy(damageInfo, body.gameObject);
                    GlobalEventManager.instance.OnHitAll(damageInfo, body.gameObject);
                }
            }

            Destroy(gameObject);
        }

        public override InterruptPriority GetMinimumInterruptPriority()
        {
            return InterruptPriority.Death;
        }
    }
}
