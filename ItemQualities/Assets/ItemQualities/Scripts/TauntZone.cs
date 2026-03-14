using HG;
using ItemQualities.Utilities;
using RoR2;
using RoR2.CharacterAI;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace ItemQualities
{
    [RequireComponent(typeof(TeamFilter))]
    public sealed class TauntZone : MonoBehaviour
    {
        public CharacterBody Attacker;

        public GameObject Target;

        [Min(0)]
        public float Interval = 1f;

        [Min(0)]
        public float Range = 10f;

        [Range(0f, 100f)]
        public float TauntChance = 60f;

        [Min(0)]
        public float HitEntitiesClearInterval = 1f;

        float _tauntTimer;
        float _hitEntitiesListClearTimer;

        SphereSearch _sphereSearch;

        List<HealthComponent> _hitEntities;

        void Awake()
        {
            if (NetworkServer.active)
            {
                _hitEntities = ListPool<HealthComponent>.RentCollection();

                _sphereSearch = new SphereSearch
                {
                    mask = LayerIndex.entityPrecise.mask,
                    queryTriggerInteraction = QueryTriggerInteraction.Ignore
                };
            }
        }

        void OnDestroy()
        {
            if (_hitEntities != null)
            {
                _hitEntities = ListPool<HealthComponent>.ReturnCollection(_hitEntities);
            }
        }

        void Start()
        {
            if (NetworkServer.active)
            {
                doTauntNearby();
            }
        }

        void FixedUpdate()
        {
            if (NetworkServer.active)
            {
                _tauntTimer += Time.fixedDeltaTime;
                if (_tauntTimer >= Interval)
                {
                    _tauntTimer = 0f;
                    doTauntNearby();
                }

                _hitEntitiesListClearTimer += Time.fixedDeltaTime;
                if (_hitEntitiesListClearTimer >= HitEntitiesClearInterval)
                {
                    _hitEntitiesListClearTimer = 0f;
                    _hitEntities.Clear();
                }
            }
        }

        void doTauntNearby()
        {
            if (!NetworkServer.active)
            {
                Log.Warning("Called on client");
                return;
            }

            _sphereSearch.origin = transform.position;
            _sphereSearch.radius = Range;

            using var _ = ListPool<HurtBox>.RentCollection(out List<HurtBox> hurtBoxes);

            _sphereSearch.RefreshCandidates()
                         .FilterCandidatesByDistinctHurtBoxEntities();

            if (Attacker)
            {
                _sphereSearch.FilterCandidatesByHurtBoxTeam(TeamMask.GetEnemyTeams(Attacker.teamComponent.teamIndex));
            }

            _sphereSearch.GetHurtBoxes(hurtBoxes);

            foreach (HurtBox hurtBox in hurtBoxes)
            {
                HealthComponent healthComponent = hurtBox ? hurtBox.healthComponent : null;
                CharacterBody body = healthComponent ? healthComponent.body : null;
                CharacterMaster master = body ? body.master : null;
                if (!body || !master)
                    continue;

                if (body.isChampion)
                    continue;

                if (body.TryGetComponent(out SetStateOnHurt setStateOnHurt) && !setStateOnHurt.canBeTaunted)
                    continue;

                if (_hitEntities.Contains(healthComponent))
                    continue;

                _hitEntities.Add(healthComponent);

                if (RollUtil.CheckRoll(TauntChance, Attacker ? Attacker.master : null, false))
                {
                    BaseAI ai = ArrayUtils.GetSafe(master.AiComponents, 0);
                    if (ai)
                    {
                        ai.currentEnemy.gameObject = Target;
                    }
                }
            }
        }

        void OnDrawGizmos()
        {
            Gizmos.DrawWireSphere(transform.position, Range);
        }
    }
}
