using RoR2.Projectile;
using UnityEngine;
using UnityEngine.Networking;

namespace ItemQualities
{
    [RequireComponent(typeof(BoomerangProjectile))]
    public sealed class BoomerangProjectileQualityController : MonoBehaviour
    {
        public float HitPauseDuration;

        private BoomerangProjectile _boomerangProjectile;

        private bool _hasStartedHitPause;

        private float _hitPauseTimer;

        public bool IsInHitPause => _hitPauseTimer > 0;

        private void Awake()
        {
            _boomerangProjectile = GetComponent<BoomerangProjectile>();
        }

        private void OnEnable()
        {
            ComponentCache.Add(gameObject, this);

            _boomerangProjectile.onFlyBack.AddListener(onFlyBack);
        }

        private void OnDisable()
        {
            _boomerangProjectile.onFlyBack.RemoveListener(onFlyBack);

            ComponentCache.Remove(gameObject, this);
        }

        private void FixedUpdate()
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

        private void onFlyBack()
        {
            tryStartHitPause();
        }

        private void tryStartHitPause()
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
