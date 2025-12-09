using RoR2;

namespace ItemQualities.Items
{
    static class PhysicsProjectile
    {
        [SystemInitializer]
        static void Init()
        {
            On.RoR2.FriendUnitController.ForceInteractibilityUpdate += FriendUnitController_ForceInteractibilityUpdate;
        }

        static void FriendUnitController_ForceInteractibilityUpdate(On.RoR2.FriendUnitController.orig_ForceInteractibilityUpdate orig, FriendUnitController self)
        {
            orig(self);

            if (self.TryGetComponent(out FriendUnitQualityController friendUnitQualityController) && friendUnitQualityController.IsQualityBehaviorActive)
            {
                self.SetInteractibility(true);
            }
        }
    }
}
