using HG.Coroutines;
using ItemQualities.ContentManagement;
using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using R2API;
using RoR2;
using RoR2BepInExPack.GameAssetPathsBetter;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace ItemQualities
{
    [RequireComponent(typeof(GenericOwnership))]
    public sealed class HealOnCritScytheHandler : MonoBehaviour
    {
        public static GameObject swingTrailPrefab = null;

        public HitBoxGroup hitBoxGroup;

        public float forceScalar;

        private GenericOwnership genericOwnership;

        private OverlapAttack _attack = null;
        private float _timer = 0;
        private bool _spawnedTrail = false;

        [ContentInitializer]
        private static IEnumerator LoadContent(ContentInitializerArgs args)
        {
            AsyncOperationHandle<GameObject> halcswingLoad = AddressableUtil.LoadAssetAsync<GameObject>(RoR2_DLC2_Halcyonite.SwingTrailHalcyonite_prefab);

            ParallelProgressCoroutine coroutine = new ParallelProgressCoroutine(args.ProgressReceiver);
            coroutine.Add(halcswingLoad);

            yield return coroutine;

            if (halcswingLoad.Status != AsyncOperationStatus.Succeeded || !halcswingLoad.Result)
            {
                Log.Error($"Failed to load halcyonite swing trail prefab: {halcswingLoad.OperationException}");
                yield break;
            }

            swingTrailPrefab = halcswingLoad.Result.InstantiateClone("swingTrail", false);
            swingTrailPrefab.transform.localScale = new Vector3(3, 1, 3);

            args.ContentPack.prefabs.Add(swingTrailPrefab);
        }

        private void Awake()
        {
            genericOwnership = GetComponent<GenericOwnership>();
        }

        private void Start()
        {
            CharacterBody ownerBody = genericOwnership.ownerObject ? genericOwnership.ownerObject.GetComponent<CharacterBody>() : null;
            if (ownerBody)
            {
                ItemQualityCounts healOnCrit;
                if (ownerBody.inventory)
                {
                    healOnCrit = ownerBody.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.HealOnCrit);
                }
                else
                {
                    healOnCrit = ItemQualityCounts.zero;
                }

                float damageCoefficient = (healOnCrit.UncommonCount * 6) +
                                          (healOnCrit.RareCount * 9) +
                                          (healOnCrit.EpicCount * 12) +
                                          (healOnCrit.LegendaryCount * 15);

                _attack = new OverlapAttack
                {
                    attacker = genericOwnership.ownerObject,
                    inflictor = gameObject,
                    teamIndex = ownerBody.teamComponent.teamIndex,
                    hitBoxGroup = hitBoxGroup,
                    damage = ownerBody.baseDamage * damageCoefficient,
                    damageType = DamageTypeCombo.Generic,
                    isCrit = true,
                    damageColorIndex = DamageColorIndex.Item,
                    attackerFiltering = AttackerFiltering.NeverHitSelf,
                    procCoefficient = 1f,
                    procChainMask = new ProcChainMask(),
                };
            }

            Util.PlaySound("Play_halcyonite_skill1_swing", gameObject);
        }
        
        private void FixedUpdate()
        {
            _timer += Time.fixedDeltaTime;

            if (_attack != null)
            {
                if (NetworkServer.active)
                {
                    _attack.forceVector = -transform.right * forceScalar;
                    _attack.Fire();
                }

                if (_timer >= 0.11f && !_spawnedTrail)
                {
                    _spawnedTrail = true;
                    GameObject trail = GameObject.Instantiate(swingTrailPrefab, transform.position, transform.rotation);
                    trail.GetComponent<ScaleParticleSystemDuration>().newDuration = 0.1f;
                }
            }

            if (_timer >= 0.2f && NetworkServer.active)
            {
                Destroy(gameObject);
            }
        }
    }
}
