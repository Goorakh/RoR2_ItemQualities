using R2API;
using RoR2;

namespace ItemQualities
{
    public static class ProcTypes
    {
        public static ModdedProcType IncreasePrimaryDamage { get; private set; } = ModdedProcType.Invalid;

        public static ModdedProcType Immobilize { get; private set; } = ModdedProcType.Invalid;

        public static ModdedProcType VoidDeathOrbProcType { get; private set; } = ModdedProcType.Invalid;

        public static ModdedProcType Bug { get; private set; } = ModdedProcType.Invalid;

        public static readonly ModdedProcType[] LifeStealOverhealProcTypes = new ModdedProcType[(int)QualityTier.Count];

        [SystemInitializer]
        static void Init()
        {
            IncreasePrimaryDamage = ProcTypeAPI.ReserveProcType();
            Immobilize = ProcTypeAPI.ReserveProcType();
            VoidDeathOrbProcType = ProcTypeAPI.ReserveProcType();
            Bug = ProcTypeAPI.ReserveProcType();

            for (QualityTier qualityTier = 0; qualityTier < QualityTier.Count; qualityTier++)
            {
                LifeStealOverhealProcTypes[(int)qualityTier] = ProcTypeAPI.ReserveProcType();
            }
        }
    }
}
