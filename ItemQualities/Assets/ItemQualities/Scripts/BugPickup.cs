using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace ItemQualities
{
    public sealed class BugPickup : MonoBehaviour
    {
        [Tooltip("The base object to destroy when this pickup is consumed.")]
        public GameObject BaseObject;

        [Tooltip("The team filter object which determines who can pick up this pack.")]
        public TeamFilter TeamFilter;

        public GameObject PickupEffect;

        [Tooltip("How much duration to add onto the users current wing usage on pickup")]
        public float JetpackDurationBonus;

        public QualityTier Tier = QualityTier.None;

        bool _alive = true;

        void OnTriggerStay(Collider other)
        {
            if (NetworkServer.active && _alive && TeamComponent.GetObjectTeam(other.gameObject) == TeamFilter.teamIndex)
            {
                CharacterBody body = other.GetComponent<CharacterBody>();
                if (body)
                {
                    JetpackController jetpackController = JetpackController.FindJetpackController(body.gameObject);
                    if (jetpackController)
                    {
                        jetpackController.duration = Mathf.Max(jetpackController.duration, jetpackController.stopwatch) + JetpackDurationBonus;
                    }

                    // TODO: Do pickup

                    body.OnPickup(CharacterBody.PickupClass.Minor);

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
