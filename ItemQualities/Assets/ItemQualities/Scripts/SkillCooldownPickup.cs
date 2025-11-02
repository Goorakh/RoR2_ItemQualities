using RoR2;
using System;
using UnityEngine;
using UnityEngine.Networking;

namespace ItemQualities
{
    public sealed class SkillCooldownPickup : MonoBehaviour
    {
        [Tooltip("The base object to destroy when this pickup is consumed.")]
        public GameObject BaseObject;

        [Tooltip("The team filter object which determines who can pick up this pack.")]
        public TeamFilter TeamFilter;

        public GameObject PickupEffect;

        public float FlatAmount;

        public float FractionalAmount;

        public SkillSlot[] ExcludeSkills = Array.Empty<SkillSlot>();

        bool _alive = true;

        void OnTriggerStay(Collider other)
        {
            if (NetworkServer.active && _alive && TeamComponent.GetObjectTeam(other.gameObject) == TeamFilter.teamIndex)
            {
                CharacterBody body = other.GetComponent<CharacterBody>();
                if (body)
                {
                    if (body.skillLocator)
                    {
                        foreach (GenericSkill skill in body.skillLocator.allSkills)
                        {
                            if (skill.baseRechargeInterval > 0 && Array.IndexOf(ExcludeSkills, body.skillLocator.FindSkillSlot(skill)) == -1)
                            {
                                applyCooldownReduction(skill);
                            }
                        }

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

        void applyCooldownReduction(GenericSkill genericSkill)
        {
            genericSkill.RunRecharge(FlatAmount + (genericSkill.cooldownRemaining * FractionalAmount));
        }
    }
}
