using RoR2;

namespace ItemQualities
{
    static class GenericSkillHooks
    {
        public delegate void GenericSkillDelegate(GenericSkill skill);
        public static event GenericSkillDelegate OnSkillRechargeAuthority;

        [SystemInitializer]
        static void Init()
        {
            On.RoR2.GenericSkill.RestockSteplike += GenericSkill_RestockSteplike;
        }

        static void GenericSkill_RestockSteplike(On.RoR2.GenericSkill.orig_RestockSteplike orig, GenericSkill self)
        {
            orig(self);
            OnSkillRechargeAuthority?.Invoke(self);
        }
    }
}
