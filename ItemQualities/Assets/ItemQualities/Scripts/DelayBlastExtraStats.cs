using HG;
using ItemQualities.Items;
using RoR2;
using RoR2.ContentManagement;
using UnityEngine;

namespace ItemQualities
{
    public sealed class DelayBlastExtraStats : MonoBehaviour
    {
        static void Init()
        {
            foreach (GameObject networkedPrefab in ContentManager.networkedObjectPrefabs)
            {
                foreach (DelayBlast delayBlast in networkedPrefab.GetComponentsInChildren<DelayBlast>())
                {
                    delayBlast.gameObject.EnsureComponent<DelayBlastExtraStats>();
                }
            }
        }

        DelayBlast _delayBlast;

        void Awake()
        {
            _delayBlast = GetComponent<DelayBlast>();
        }

        void Start()
        {
            if (_delayBlast)
            {
                GameObject attacker = _delayBlast.attacker;
                if (_delayBlast.blastAttackOverride != null)
                {
                    attacker = _delayBlast.blastAttackOverride.attacker;
                }

                if (attacker && attacker.TryGetComponent(out CharacterBody attackerBody))
                {
                    _delayBlast.radius = ExplodeOnDeath.GetExplosionRadius(_delayBlast.radius, attackerBody);

                    if (_delayBlast.blastAttackOverride != null)
                    {
                        _delayBlast.blastAttackOverride.radius = ExplodeOnDeath.GetExplosionRadius(_delayBlast.blastAttackOverride.radius, attackerBody);
                    }

                    if (_delayBlast.effectDataOverride != null)
                    {
                        _delayBlast.effectDataOverride.scale = ExplodeOnDeath.GetExplosionRadius(_delayBlast.effectDataOverride.scale, attackerBody);
                    }
                }
            }
        }
    }
}
