using RoR2.Orbs;
using UnityEngine;

namespace ItemQualities.Orbs
{
    public sealed class BugOrb : GenericDamageOrb
    {
        public override GameObject GetOrbEffect()
        {
            return ItemQualitiesContent.Prefabs.BugOrbEffect;
        }

        public override void Begin()
        {
            speed = 60f;
            base.Begin();
        }
    }
}
