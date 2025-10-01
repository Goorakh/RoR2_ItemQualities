using ItemQualities;
using RoR2;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace EntityStates.BossGroupHealNovaController
{
    public class BossGroupHealNovaPulse : EntityState
    {
        public static AnimationCurve NovaRadiusCurve;

        public static float GrowDuration;

        public static float LingerDuration;

        float _totalDuration;

        Transform _effectTransform;

        HealPulse _healPulse;

        float _radius;

        public override void OnEnter()
        {
            base.OnEnter();

            _totalDuration = GrowDuration + LingerDuration;

            if (transform.parent && transform.parent.TryGetComponent(out BossGroupHealNovaSpawner healNovaSpawner))
            {
                _radius = healNovaSpawner.NovaRadius;
            }

            TeamFilter teamFilter = GetComponent<TeamFilter>();
            TeamIndex teamIndex = teamFilter ? teamFilter.teamIndex : TeamIndex.None;

            if (NetworkServer.active)
            {
                _healPulse = new HealPulse(transform.position, _radius, 0.5f, GrowDuration, teamIndex);
            }

            _effectTransform = transform.Find("PulseEffect");

            if (_effectTransform)
            {
                _effectTransform.gameObject.SetActive(true);
            }
        }

        public override void OnExit()
        {
            if (_effectTransform)
            {
                _effectTransform.gameObject.SetActive(false);
            }

            base.OnExit();
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();

            if (NetworkServer.active)
            {
                _healPulse.Update(GetDeltaTime());

                if (fixedAge >= _totalDuration)
                {
                    Destroy(outer.gameObject);
                }
            }
        }

        public override void Update()
        {
            if (_effectTransform)
            {
                float radius = _radius * NovaRadiusCurve.Evaluate(Mathf.Clamp01(fixedAge / GrowDuration));
                _effectTransform.localScale = new Vector3(radius, radius, radius);
            }
        }

        class HealPulse
        {
            readonly HashSet<HealthComponent> _healedTargets = new HashSet<HealthComponent>();

            readonly SphereSearch _sphereSearch;

            readonly float _duration;

            readonly float _finalRadius;

            readonly float _healFractionValue;

            readonly TeamMask _teamMask;

            readonly List<HurtBox> _hurtBoxesList = new List<HurtBox>();

            float _timeElapsed;

            public HealPulse(Vector3 origin, float finalRadius, float healFractionValue, float duration, TeamIndex teamIndex)
            {
                _sphereSearch = new SphereSearch
                {
                    mask = LayerIndex.entityPrecise.mask,
                    origin = origin,
                    queryTriggerInteraction = QueryTriggerInteraction.Collide,
                    radius = 0f
                };

                _finalRadius = finalRadius;
                _healFractionValue = healFractionValue;
                _duration = duration;

                _teamMask = new TeamMask();
                _teamMask.AddTeam(teamIndex);
            }

            public void Update(float deltaTime)
            {
                _timeElapsed += deltaTime;
                float fractionComplete = Mathf.Clamp01(_timeElapsed / _duration);

                _sphereSearch.radius = _finalRadius * NovaRadiusCurve.Evaluate(fractionComplete);

                _sphereSearch.RefreshCandidates()
                             .FilterCandidatesByHurtBoxTeam(_teamMask)
                             .FilterCandidatesByDistinctHurtBoxEntities()
                             .GetHurtBoxes(_hurtBoxesList);

                foreach (HurtBox hurtBox in _hurtBoxesList)
                {
                    if (_healedTargets.Add(hurtBox.healthComponent))
                    {
                        healTarget(hurtBox.healthComponent);
                    }
                }

                _hurtBoxesList.Clear();
            }

            void healTarget(HealthComponent target)
            {
                target.HealFraction(_healFractionValue, new ProcChainMask());
                Util.PlaySound("Play_item_proc_TPhealingNova_hitPlayer", target.gameObject);
            }
        }
    }
}
