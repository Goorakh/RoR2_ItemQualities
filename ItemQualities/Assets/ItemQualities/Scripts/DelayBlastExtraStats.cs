using HG;
using ItemQualities.Items;
using RoR2;
using RoR2.ContentManagement;
using UnityEngine;

namespace ItemQualities
{
    public sealed class DelayBlastExtraStats : MonoBehaviour
    {
        [SystemInitializer]
        private static void Init()
        {
            On.RoR2.DelayBlast.Awake += DelayBlast_Awake;
        }

        private static void DelayBlast_Awake(On.RoR2.DelayBlast.orig_Awake orig, DelayBlast self)
        {
            orig(self);
            self.gameObject.EnsureComponent<DelayBlastExtraStats>();
        }

        private DelayBlast _delayBlast;

        private void Awake()
        {
            _delayBlast = GetComponent<DelayBlast>();
        }

        private void Start()
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
