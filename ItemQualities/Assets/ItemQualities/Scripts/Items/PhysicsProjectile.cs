using RoR2;

namespace ItemQualities.Items
{
    internal static class PhysicsProjectile
    {
        [SystemInitializer]
        private static void Init()
        {
            On.RoR2.FriendUnitController.ForceInteractibilityUpdate += FriendUnitController_ForceInteractibilityUpdate;
        }

        private static void FriendUnitController_ForceInteractibilityUpdate(On.RoR2.FriendUnitController.orig_ForceInteractibilityUpdate orig, FriendUnitController self)
        {
            orig(self);

            if (self.TryGetComponent(out FriendUnitQualityController friendUnitQualityController))
            {
                bool baseIsInteractable = self.genericInteraction && self.genericInteraction.interactability == Interactability.Available;

                if (friendUnitQualityController.IsQualityBehaviorActive)
                {
                    self.SetInteractibility(true);
                }

                if (friendUnitQualityController.InteractionProcFilter)
                {
                    friendUnitQualityController.InteractionProcFilter.shouldAllowOnInteractionBeginProc = baseIsInteractable;
                }
            }
        }
    }
}
