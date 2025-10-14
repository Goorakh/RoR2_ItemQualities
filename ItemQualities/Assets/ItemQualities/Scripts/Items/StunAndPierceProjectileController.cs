using HG;
using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using RoR2;
using RoR2.Projectile;
using RoR2BepInExPack.GameAssetPathsBetter;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace ItemQualities.Items
{
    public class StunAndPierceProjectileController : NetworkBehaviour
    {
        [SystemInitializer]
        static void Init()
        {
            AddressableUtil.LoadTempAssetAsync<GameObject>(RoR2_DLC2_Items_StunAndPierce.StunAndPierceBoomerang_prefab).OnSuccess(boomerangPrefab =>
            {
                boomerangPrefab.EnsureComponent<StunAndPierceProjectileController>();
            });
        }

        ProjectileController _projectileController;
        BoomerangProjectile _boomerangProjectile;
        ProjectileOverlapAttack _projectileOverlapAttack;

        float _startBounceTimer = -1f;

        int _bouncesRemaining = 0;

        GameObject _lastHitObject;

        readonly List<GameObject> _bouncedObjects = new List<GameObject>();

        readonly BullseyeSearch _bounceTargetSearch = new BullseyeSearch
        {
            queryTriggerInteraction = QueryTriggerInteraction.Ignore,
            filterByDistinctEntity = true,
            filterByLoS = true,
            sortMode = BullseyeSearch.SortMode.DistanceAndAngle,
            maxAngleFilter = 360f,
            teamMaskFilter = TeamMask.all
        };

        void Awake()
        {
            _projectileController = GetComponent<ProjectileController>();
            _boomerangProjectile = GetComponent<BoomerangProjectile>();
            _projectileOverlapAttack = GetComponent<ProjectileOverlapAttack>();

            _projectileController.onInitialized += onInitialized;
        }

        void onInitialized(ProjectileController projectileController)
        {
            if (projectileController.owner)
            {
                _bounceTargetSearch.teamMaskFilter = TeamMask.GetEnemyTeams(TeamComponent.GetObjectTeam(projectileController.owner));

                if (projectileController.owner.TryGetComponent(out CharacterBody ownerBody))
                {
                    ItemQualityCounts stunAndPierce = ItemQualitiesContent.ItemQualityGroups.StunAndPierce.GetItemCounts(ownerBody.inventory);

                    float bounceChance = (30f * stunAndPierce.UncommonCount) +
                                         (50f * stunAndPierce.RareCount) +
                                         (80f * stunAndPierce.EpicCount) +
                                         (100f * stunAndPierce.LegendaryCount);

                    _bouncesRemaining = RollUtil.GetOverflowRoll(bounceChance, ownerBody.master);
                }
            }
        }

        void OnEnable()
        {
            _projectileOverlapAttack.onServerHitGameObject.AddListener(onHit);
        }

        void OnDisable()
        {
            _projectileOverlapAttack.onServerHitGameObject.RemoveListener(onHit);
            _bouncedObjects.Clear();
        }

        void FixedUpdate()
        {
            if (_startBounceTimer > 0f)
            {
                _startBounceTimer -= Time.fixedDeltaTime;
                if (_startBounceTimer <= 0f)
                {
                    tryRedirectBoomerang();
                }
            }
        }

        void onHit(GameObject hitObject)
        {
            if (!NetworkServer.active)
                return;

            Log.Debug($"{Util.GetGameObjectHierarchyName(gameObject)} ({_boomerangProjectile.boomerangState}) hit {Util.GetGameObjectHierarchyName(hitObject)}");

            if (_boomerangProjectile.boomerangState != BoomerangProjectile.BoomerangState.FlyOut)
                return;

            if (!hitObject || _lastHitObject == hitObject)
                return;

            if (_bouncesRemaining > 0)
            {
                _lastHitObject = hitObject;

                if (_startBounceTimer <= 0f)
                {
                    _startBounceTimer = 0.4f;
                }
            }
        }

        [Server]
        void tryRedirectBoomerang()
        {
            if (_bouncesRemaining <= 0)
                return;

            _bouncesRemaining--;

            _bounceTargetSearch.searchOrigin = transform.position;
            _bounceTargetSearch.searchDirection = transform.forward;
            _bounceTargetSearch.maxDistanceFilter = _boomerangProjectile.travelSpeed * _boomerangProjectile.maxFlyStopwatch;

            _bounceTargetSearch.RefreshCandidates();

            GameObject targetObject = null;
            foreach (HurtBox hurtBox in _bounceTargetSearch.GetResults())
            {
                HealthComponent targetHealthComponent = hurtBox ? hurtBox.healthComponent : null;
                if (targetHealthComponent && targetHealthComponent.gameObject != _lastHitObject && !_bouncedObjects.Contains(targetHealthComponent.gameObject))
                {
                    targetObject = targetHealthComponent.gameObject;
                    break;
                }
            }

            if (targetObject)
            {
                _bouncedObjects.Add(targetObject);

                Vector3 targetPosition = targetObject.transform.position;
                if (targetObject.TryGetComponent(out CharacterBody body))
                {
                    targetPosition = body.corePosition;
                }

                transform.forward = (targetPosition - transform.position).normalized;

                resetBoomerang();
            }
        }

        void resetBoomerang()
        {
            _boomerangProjectile.NetworkboomerangState = BoomerangProjectile.BoomerangState.FlyOut;
            _boomerangProjectile.stopwatch = 0f;
        }
    }
}
