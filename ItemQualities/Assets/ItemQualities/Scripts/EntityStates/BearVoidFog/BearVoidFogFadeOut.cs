using RoR2;

namespace EntityStates.BearVoidFog
{
    public sealed class BearVoidFogFadeOut : EntityState
    {
        public static float duration;

        public override void OnEnter()
        {
            base.OnEnter();

            if (TryGetComponent(out BuffWard buffWard))
            {
                buffWard.enabled = false;

                // Undo indicator hide by BuffWard.OnDisable
                if (buffWard.rangeIndicator)
                {
                    buffWard.rangeIndicator.gameObject.SetActive(true);
                }
            }

            if (TryGetComponent(out ChildLocator childLocator) &&
                childLocator.TryFindChildComponent("Indicator", out AnimateShaderAlpha fadeOutController))
            {
                fadeOutController.enabled = true;
                fadeOutController.timeMax = duration;
            }
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();

            if (isAuthority && fixedAge >= duration)
            {
                Destroy(gameObject);
            }
        }
    }
}
