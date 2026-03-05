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
        [Min(0)]
        public float JetpackDurationBonus;

        public BuffQualityGroup BuffGroup;

        public QualityTier Tier = QualityTier.None;

        bool _alive = true;

        void OnTriggerStay(Collider other)
        {
            if (NetworkServer.active && _alive && TeamComponent.GetObjectTeam(other.gameObject) == TeamFilter.teamIndex)
            {
                CharacterBody body = other.GetComponent<CharacterBody>();
                if (body)
                {
                    if (JetpackDurationBonus > 0)
                    {
                        JetpackController jetpackController = JetpackController.FindJetpackController(body.gameObject);
                        if (jetpackController)
                        {
                            jetpackController.duration = Mathf.Max(jetpackController.duration, jetpackController.stopwatch) + JetpackDurationBonus;
                            jetpackController.providingAntiGravity = true;
                            jetpackController.providingFlight = true;
                        }
                    }

                    if (BuffGroup)
                    {
                        BuffIndex buffIndex = BuffGroup.GetBuffIndex(Tier);
                        if (buffIndex != BuffIndex.None)
                        {
                            body.AddBuff(buffIndex);
                        }
                    }

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
