using ItemQualities;
using RoR2;

namespace EntityStates.MushroomShield
{
    public sealed class MushroomBubbleFlashOut : MushroomBubbleBaseState
    {
        static EffectIndex _bubbleShieldEndEffect = EffectIndex.Invalid;

        [SystemInitializer(typeof(EffectCatalogUtils))]
        static void Init()
        {
            _bubbleShieldEndEffect = EffectCatalogUtils.FindEffectIndex("BubbleShieldEndEffect");
            if (_bubbleShieldEndEffect == EffectIndex.Invalid)
            {
                Log.Warning("Failed to find charge effect index");
            }
        }

        public static string EndSoundString;

        public static float Duration;

        BeginRapidlyActivatingAndDeactivating _blinkController;

        public override void OnEnter()
        {
            base.OnEnter();
            _blinkController = GetComponent<BeginRapidlyActivatingAndDeactivating>();
            if (_blinkController)
            {
                _blinkController.delayBeforeBeginningBlinking = 0f;
                _blinkController.fixedAge = 0f;
                _blinkController.enabled = true;
            }
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();

            if (isAuthority && fixedAge >= Duration)
            {
                Destroy(gameObject);
            }
        }

        public override void OnExit()
        {
            base.OnExit();

            EffectManager.SpawnEffect(_bubbleShieldEndEffect, new EffectData
            {
                origin = transform.position,
                rotation = transform.rotation,
                scale = EffectRadius
            }, false);

            Util.PlaySound(EndSoundString, gameObject);

            if (_blinkController)
            {
                _blinkController.enabled = false;
            }
        }

        public override void Undeploy(bool immediate)
        {
            // Already undeploying, nothing to do
        }
    }
}
