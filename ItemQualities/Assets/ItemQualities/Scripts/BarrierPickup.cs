using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace ItemQualities
{
    public sealed class BarrierPickup : MonoBehaviour
    {
        [Tooltip("The base object to destroy when this pickup is consumed.")]
        public GameObject BaseObject;

        [Tooltip("The team filter object which determines who can pick up this pack.")]
        public TeamFilter TeamFilter;

        public GameObject PickupEffect;

        public float FlatAmount;

        public float FractionalAmount;

        bool _alive = true;

        void OnTriggerStay(Collider other)
        {
            if (NetworkServer.active && _alive && TeamComponent.GetObjectTeam(other.gameObject) == TeamFilter.teamIndex)
            {
                CharacterBody body = other.GetComponent<CharacterBody>();
                if (body)
                {
                    HealthComponent healthComponent = body.healthComponent;
                    if (healthComponent)
                    {
                        healthComponent.AddBarrier(FlatAmount + (healthComponent.fullBarrier * FractionalAmount));

                        if (PickupEffect)
                        {
                            EffectManager.SpawnEffect(PickupEffect, new EffectData
                            {
                                origin = transform.position
                            }, true);
                        }
                    }

                    Destroy(BaseObject);
                    _alive = false;
                }
            }
        }
    }
}
