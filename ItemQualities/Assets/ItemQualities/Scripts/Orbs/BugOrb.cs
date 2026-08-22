using RoR2.Orbs;
using UnityEngine;

namespace ItemQualities.Orbs
{
    public sealed class BugOrb : GenericDamageOrb
    {
        private static readonly float _baseSpeed = 60f;

        public override GameObject GetOrbEffect()
        {
            return ItemQualitiesContent.Prefabs.BugOrbEffect;
        }

        public override void Begin()
        {
            speed = _baseSpeed;
            base.Begin();
        }
    }
}
