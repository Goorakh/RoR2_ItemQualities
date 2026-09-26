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
    public class HealOnCritScytheHandler : MonoBehaviour
    {
        public static GameObject swingTrailPrefab = null;

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

            swingTrailPrefab = halcswingLoad.Result.InstantiateClone("swingTrail");
            swingTrailPrefab.transform.localScale = new Vector3(3, 1, 3);

            args.ContentPack.prefabs.Add(swingTrailPrefab);
        }

        private void Start()
        {
            if (!transform.parent || !transform.parent.TryGetComponent(out GenericOwnership ownership))
                return;
            if (!ownership.ownerObject || !ownership.ownerObject.TryGetComponent(out CharacterBody body))
                return;
            if (!body.inventory)
                return;

            ItemQualityCounts healOnCrit = body.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.HealOnCrit);
            float damageCoeff = healOnCrit.UncommonCount * 6 +
                                healOnCrit.RareCount * 9 +
                                healOnCrit.EpicCount * 12 +
                                healOnCrit.LegendaryCount * 15;

            _attack = new OverlapAttack();
            _attack.attacker = ownership.ownerObject;
            _attack.inflictor = ownership.ownerObject;
            _attack.teamIndex = TeamComponent.GetObjectTeam(_attack.attacker);
            _attack.damage = body.baseDamage * damageCoeff;
            _attack.isCrit = true;
            _attack.hitBoxGroup = GetComponent<HitBoxGroup>();
        }
        
        private void FixedUpdate()
        {
            if (NetworkServer.active)
            {
                _attack?.Fire();
            }

            if (_timer == 0)
            {
                Util.PlaySound("Play_halcyonite_skill1_swing", gameObject);
            }
            
            if (_timer >= 0.11f && !_spawnedTrail)
            {
                _spawnedTrail = true;
                GameObject trail = GameObject.Instantiate(swingTrailPrefab, transform.position, transform.rotation);
                trail.GetComponent<ScaleParticleSystemDuration>().newDuration = 0.1f;
            }
            _timer += Time.fixedDeltaTime;
        }

        private void DestroyGameObject()
        {
            Destroy(gameObject);
        }
    }
}
