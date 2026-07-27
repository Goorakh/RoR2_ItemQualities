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

        public static ModdedProcType Crowbar { get; private set; } = ModdedProcType.Invalid;

        public static ModdedProcType Knurl { get; private set; } = ModdedProcType.Invalid;

        [SystemInitializer]
        private static void Init()
        {
            IncreasePrimaryDamage = ProcTypeAPI.ReserveProcType();
            Immobilize = ProcTypeAPI.ReserveProcType();
            VoidDeathOrbProcType = ProcTypeAPI.ReserveProcType();
            Bug = ProcTypeAPI.ReserveProcType();
            Crowbar = ProcTypeAPI.ReserveProcType();
            Knurl = ProcTypeAPI.ReserveProcType();
        }
    }
}
