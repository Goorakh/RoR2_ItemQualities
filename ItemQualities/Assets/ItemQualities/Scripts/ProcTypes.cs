using R2API;
using RoR2;

namespace ItemQualities
{
    public static class ProcTypes
    {
        public static ModdedProcType IncreasePrimaryDamage { get; private set; } = ModdedProcType.Invalid;
        public static ModdedProcType Immobilize { get; private set; } = ModdedProcType.Invalid;

        [SystemInitializer]
        static void Init()
        {
            IncreasePrimaryDamage = ProcTypeAPI.ReserveProcType();
            Immobilize = ProcTypeAPI.ReserveProcType();
        }
    }
}
