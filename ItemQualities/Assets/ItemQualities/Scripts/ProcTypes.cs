using R2API;
using RoR2;

namespace ItemQualities
{
    public static class ProcTypes
    {
        public static ModdedProcType IncreasePrimaryDamage { get; private set; } = ModdedProcType.Invalid;

        [SystemInitializer]
        static void Init()
        {
            IncreasePrimaryDamage = ProcTypeAPI.ReserveProcType();
        }
    }
}
