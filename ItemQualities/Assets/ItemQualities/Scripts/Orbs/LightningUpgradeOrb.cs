using RoR2.Orbs;

namespace ItemQualities.Orbs
{
    public class LightningUpgradeOrb : LightningStrikeOrb
    {
        public override void Begin()
        {
            base.Begin();
            base.duration = 0f;
        }
    }
}
