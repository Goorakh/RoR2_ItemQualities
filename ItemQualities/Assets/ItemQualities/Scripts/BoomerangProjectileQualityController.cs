using RoR2.Projectile;
using UnityEngine;
using UnityEngine.Networking;

namespace ItemQualities
{
    [RequireComponent(typeof(BoomerangProjectile))]
    public sealed class BoomerangProjectileQualityController : MonoBehaviour
    {
        public float HitPauseDuration;

        BoomerangProjectile _boomerangProjectile;

        bool _hasStartedHitPause;

        float _hitPauseTimer;

        public bool IsInHitPause => _hitPauseTimer > 0;

        void Awake()
        {
            _boomerangProjectile = GetComponent<BoomerangProjectile>();
        }

        void OnEnable()
        {
            ComponentCache.Add(gameObject, this);

            _boomerangProjectile.onFlyBack.AddListener(onFlyBack);
        }

        void OnDisable()
        {
            _boomerangProjectile.onFlyBack.RemoveListener(onFlyBack);

            ComponentCache.Remove(gameObject, this);
        }

        void FixedUpdate()
        {
            if (NetworkServer.active)
            {
                if (IsInHitPause)
                {
                    _hitPauseTimer -= Time.fixedDeltaTime;
                }
                else if (!_hasStartedHitPause)
                {
                    if (_boomerangProjectile.boomerangState == BoomerangProjectile.BoomerangState.Transition)
                    {
                        if (_boomerangProjectile.stopwatch >= _boomerangProjectile.transitionDuration / 2f)
                        {
                            tryStartHitPause();
                        }
                    }
                }
            }
        }

        void onFlyBack()
        {
            tryStartHitPause();
        }

        void tryStartHitPause()
        {
            if (!_hasStartedHitPause)
            {
                _hasStartedHitPause = true;

                _hitPauseTimer = Mathf.Max(_hitPauseTimer, HitPauseDuration);
                _boomerangProjectile.rigidbody.velocity = Vector3.zero;
            }
        }
    }
}
