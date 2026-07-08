using RoR2;
using RoR2BepInExPack.Utilities;

namespace ItemQualities.Utilities.Extensions
{
    internal static class DamageInfoExtensions
    {
        private static readonly FixedConditionalWeakTable<DamageInfo, DamageInfoData> _damageInfoDataLookup = new FixedConditionalWeakTable<DamageInfo, DamageInfoData>();

        [SystemInitializer]
        private static void Init()
        {
            On.RoR2.HealthComponent.ProcParry += HealthComponent_ProcParry;
        }

        private static void HealthComponent_ProcParry(On.RoR2.HealthComponent.orig_ProcParry orig, HealthComponent self, DamageInfo damageInfo)
        {
            orig(self, damageInfo);

            DamageInfoData damageInfoData = _damageInfoDataLookup.GetOrCreateValue(damageInfo);
            damageInfoData.Parried = true;
        }

        public static bool IsParried(this DamageInfo damageInfo)
        {
            return _damageInfoDataLookup.TryGetValue(damageInfo, out DamageInfoData damageInfoData) && damageInfoData.Parried;
        }

        private sealed class DamageInfoData
        {
            public bool Parried;
        }
    }
}
