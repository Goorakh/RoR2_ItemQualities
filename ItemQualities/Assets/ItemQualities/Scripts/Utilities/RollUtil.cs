using RoR2;

namespace ItemQualities.Utilities
{
    public static class RollUtil
    {
        public static int GetOverflowRoll(float percentChance, CharacterMaster master)
        {
            int roll = (int)(percentChance / 100f);

            if (Util.CheckRoll(percentChance % 100f, master))
            {
                roll++;
            }

            return roll;
        }
    }
}
