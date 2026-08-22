using ItemQualities;
using ItemQualities.ContentManagement;
using ItemQualities.Items;
using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using R2API;
using RoR2;
using RoR2BepInExPack.GameAssetPathsBetter;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace EntityStates.VagrantNovaItemQuality
{
    public sealed class DetonateState : BaseVagrantNovaItemQualityState
    {
        public static GameObject explosionEffectPrefab { get; private set; }

        [ContentInitializer]
        private static IEnumerator LoadContent(ContentInitializerArgs args)
        {
            AsyncOperationHandle<GameObject> vagrantNovaExplosionLoad = AddressableUtil.LoadTempAssetAsync<GameObject>(RoR2_Base_Vagrant.VagrantNovaExplosion_prefab);
            vagrantNovaExplosionLoad.OnSuccess(vagrantNovaExplosionPrefab =>
            {
                explosionEffectPrefab = EffectScalingFixer.CreateFixedScalingCopy(vagrantNovaExplosionPrefab, 40f, "MiniVagrantNovaExplosion");

                Light light = explosionEffectPrefab.GetComponentInChildren<Light>();
                if (light)
                {
                    light.gameObject.SetActive(false);
                }

                PostProcessVolume ppVolume = explosionEffectPrefab.GetComponentInChildren<PostProcessVolume>();
                if (ppVolume)
                {
                    ppVolume.gameObject.SetActive(false);
                }

                args.ContentPack.effectDefs.Add(new EffectDef(explosionEffectPrefab));
            });

            return vagrantNovaExplosionLoad.AsProgressCoroutine(args.ProgressReceiver);
        }

        public static float blastRadius;

        public static float blastProcCoefficient;

        public static float blastForce;

        public static float baseDuration;

        public static string explosionSound;

        private float duration;

        public override void OnEnter()
        {
            base.OnEnter();

            duration = baseDuration;

            float radius = ExplodeOnDeath.GetExplosionRadius(blastRadius, attachedBody);

            if (NetworkServer.active && attachedBody)
            {
                ItemQualityCounts itemCounts = GetItemCounts();
                if (itemCounts.TotalQualityCount == 0)
                {
                    itemCounts.UncommonCount = 1;
                }

                new BlastAttack
                {
                    attacker = attachedBody.gameObject,
                    baseDamage = attachedBody.damage * NovaOnLowHealth.GetMiniNovaDamageCoefficient(itemCounts),
                    baseForce = blastForce,
                    bonusForce = Vector3.zero,
                    attackerFiltering = AttackerFiltering.NeverHitSelf,
                    crit = attachedBody.RollCrit(),
                    damageColorIndex = DamageColorIndex.Item,
                    damageType = DamageTypeExtended.Electrical,
                    falloffModel = BlastAttack.FalloffModel.None,
                    inflictor = gameObject,
                    position = attachedBody.corePosition,
                    procChainMask = new ProcChainMask(),
                    procCoefficient = blastProcCoefficient,
                    radius = radius,
                    losType = BlastAttack.LoSType.NearestHit,
                    teamIndex = attachedBody.teamComponent.teamIndex,
                }.Fire();
            }

            EffectData effectData = new EffectData
            {
                origin = attachedBody ? attachedBody.corePosition : transform.position,
                scale = radius,
            };

            if (attachedBody)
            {
                effectData.SetHurtBoxReference(attachedBody.mainHurtBox);
            }

            EffectManager.SpawnEffect(explosionEffectPrefab, effectData, false);

            Util.PlaySound(explosionSound, gameObject);
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();
            if (isAuthority && fixedAge >= duration)
            {
                outer.SetNextStateToMain();
            }
        }
    }
}
