using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace ItemQualities
{
    [RequireComponent(typeof(NetworkedBodyAttachment))]
    [RequireComponent(typeof(HealthComponent))]
    public sealed class RepeatDeathEvent : MonoBehaviour
    {
        private static EffectIndex _deathEventTickEffectIndex = EffectIndex.Invalid;

        [SystemInitializer(typeof(EffectCatalogUtils))]
        private static void Init()
        {
            _deathEventTickEffectIndex = EffectCatalogUtils.FindEffectIndex("DeathProjectileTickEffect");
            if (_deathEventTickEffectIndex == EffectIndex.Invalid)
            {
                Log.Warning("Failed to find death tick effect index");
            }
        }

        public int RemainingDeathEvents = 1;

        public float DelayBetweenDeathEvents = 1f;

        private NetworkedBodyAttachment _bodyAttachment;

        private HealthComponent _healthComponent;

        private float _timer = 0f;

        private void Awake()
        {
            _bodyAttachment = GetComponent<NetworkedBodyAttachment>();
            _healthComponent = GetComponent<HealthComponent>();
        }

        private void FixedUpdate()
        {
            if (NetworkServer.active)
            {
                _timer += Time.fixedDeltaTime;
                if (_timer > DelayBetweenDeathEvents)
                {
                    if (RemainingDeathEvents == 0)
                    {
                        Destroy(gameObject);
                    }
                    else
                    {
                        _timer -= DelayBetweenDeathEvents;
                        RemainingDeathEvents--;

                        tickDeathEvent();
                    }
                }
            }
        }

        private void tickDeathEvent()
        {
            CharacterBody body = _bodyAttachment.attachedBody;

            Vector3 position = body ? body.corePosition : transform.position;

            if (_deathEventTickEffectIndex != EffectIndex.Invalid)
            {
                EffectData effectData = new EffectData
                {
                    origin = position,
                    rotation = Quaternion.identity
                };

                EffectManager.SpawnEffect(_deathEventTickEffectIndex, effectData, true);
            }

            DamageInfo damageInfo = new DamageInfo
            {
                attacker = body ? body.gameObject : null,
                damage = body ? body.damage : 0f,
                crit = body ? body.RollCrit() : false,
                procCoefficient = 0f,
                position = position,
                damageColorIndex = DamageColorIndex.Item
            };

            DamageReport damageReport = new DamageReport(damageInfo, _healthComponent, damageInfo.damage, _healthComponent.combinedHealth);
            GlobalEventManager.instance.OnCharacterDeath(damageReport);
        }
    }
}
