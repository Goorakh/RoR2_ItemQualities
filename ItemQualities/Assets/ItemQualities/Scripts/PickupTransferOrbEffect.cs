using HG;
using ItemQualities.ContentManagement;
using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using R2API;
using RoR2;
using RoR2.Orbs;
using RoR2BepInExPack.GameAssetPathsBetter;
using System.Collections;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace ItemQualities
{
    public sealed class PickupTakenOrbEffect : MonoBehaviour
    {
        [ContentInitializer]
        private static IEnumerator LoadContent(ContentInitializerArgs args)
        {
            AsyncOperationHandle<GameObject> itemTransferOrbEffectLoad = AddressableUtil.LoadTempAssetAsync<GameObject>(RoR2_Base_Common_VFX.ItemTransferOrbEffect_prefab);
            itemTransferOrbEffectLoad.OnSuccess(itemTransferOrbEffect =>
            {
                GameObject pickupTransferOrbEffectPrefab = itemTransferOrbEffect.InstantiateClone(nameof(ItemQualitiesContent.Prefabs.PickupTransferOrbEffect), false);

                if (pickupTransferOrbEffectPrefab.TryGetComponent(out ItemTakenOrbEffect itemTakenOrbEffect))
                {
                    PickupTakenOrbEffect pickupTakenOrbEffect = pickupTransferOrbEffectPrefab.AddComponent<PickupTakenOrbEffect>();

                    pickupTakenOrbEffect.trailToColor = itemTakenOrbEffect.trailToColor;
                    pickupTakenOrbEffect.particlesToColor = ArrayUtils.Clone(itemTakenOrbEffect.particlesToColor);
                    pickupTakenOrbEffect.spritesToColor = ArrayUtils.Clone(itemTakenOrbEffect.spritesToColor);
                    pickupTakenOrbEffect.iconSpriteRenderer = itemTakenOrbEffect.iconSpriteRenderer;

                    Destroy(itemTakenOrbEffect);
                }
                else
                {
                    Log.Error($"{pickupTransferOrbEffectPrefab} is missing ItemTakenOrbEffect component");
                }

                args.ContentPack.prefabs.Add(pickupTransferOrbEffectPrefab);
                args.ContentPack.effectDefs.Add(new EffectDef(pickupTransferOrbEffectPrefab));
            });

            return itemTransferOrbEffectLoad.AsProgressCoroutine(args.ProgressReceiver);
        }

        public TrailRenderer trailToColor;

        public ParticleSystem[] particlesToColor;

        public SpriteRenderer[] spritesToColor;

        public SpriteRenderer iconSpriteRenderer;

        private EffectComponent effectComponent;

        private void Awake()
        {
            effectComponent = GetComponent<EffectComponent>();
        }

        public void OnEnable()
        {
            StartCoroutine(DelayedUpdateSprite());
        }

        public IEnumerator DelayedUpdateSprite()
        {
            yield return 0;

            PickupIndex pickupIndex = effectComponent && effectComponent.effectData != null ? new PickupIndex(Util.UintToIntMinusOne(effectComponent.effectData.genericUInt)) : PickupIndex.none;
            PickupDef pickupDef = PickupCatalog.GetPickupDef(pickupIndex);

            Color color = ColorCatalog.GetColor(ColorCatalog.ColorIndex.Error);
            Sprite sprite = null;
            if (pickupDef != null)
            {
                color = pickupDef.baseColor;
                sprite = pickupDef.iconSprite;
            }

            if (trailToColor)
            {
                trailToColor.startColor *= color;
                trailToColor.endColor *= color;
            }

            foreach (ParticleSystem particleSystem in particlesToColor)
            {
                ParticleSystem.MainModule main = particleSystem.main;
                main.startColor = color;

                particleSystem.Play();
            }

            foreach (SpriteRenderer spriteRenderer in spritesToColor)
            {
                spriteRenderer.color = color;
            }

            iconSpriteRenderer.sprite = sprite;
        }
    }
}
