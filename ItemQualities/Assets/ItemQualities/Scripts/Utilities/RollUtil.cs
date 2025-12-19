using RoR2;

namespace ItemQualities.Utilities
{
    public static class RollUtil
    {
        public static int GetOverflowRoll(float percentChance, CharacterMaster master, bool sureProc)
        {
            int roll = (int)(percentChance / 100f);

            if (CheckRoll(percentChance % 100f, master, sureProc))
            {
                roll++;
            }

            return roll;
        }

        public static bool CheckRoll(float percentChance, CharacterMaster master, bool sureProc)
        {
            return (sureProc && percentChance > 0f) || Util.CheckRoll(percentChance, master);
        }
    }
}
