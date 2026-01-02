using ItemQualities.Utilities.Extensions;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace ItemQualities
{
    public sealed class SpeedOnPickupStatsPickup : MonoBehaviour
    {
        [Tooltip("The base object to destroy when this pickup is consumed.")]
        public GameObject BaseObject;

        [Tooltip("The team filter object which determines who can pick up this pack.")]
        public TeamFilter TeamFilter;

        public GameObject PickupEffect;

        public int BuffStacks = 1;

        bool _alive = true;

        void OnTriggerStay(Collider other)
        {
            if (NetworkServer.active && _alive && TeamComponent.GetObjectTeam(other.gameObject) == TeamFilter.teamIndex)
            {
                if (other.TryGetComponent(out CharacterBody body) &&
                    body.master &&
                    body.master.TryGetComponentCached(out CharacterMasterExtraStatsTracker masterExtraStats))
                {
                    body.OnPickup(CharacterBody.PickupClass.Minor);

                    masterExtraStats.SpeedOnPickupBonus += BuffStacks;

                    if (PickupEffect)
                    {
                        EffectManager.SpawnEffect(PickupEffect, new EffectData
                        {
                            origin = transform.position
                        }, true);
                    }

                    Destroy(BaseObject);

                    _alive = false;
                }
            }
        }
    }
}
