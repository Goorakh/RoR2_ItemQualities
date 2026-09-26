using AK.Wwise;
using HG.Coroutines;
using ItemQualities.ContentManagement;
using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using R2API;
using RoR2;
using RoR2BepInExPack.GameAssetPathsBetter;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace ItemQualities.Items
{
    public sealed class HealOnCritQualityItemBehavior : QualityItemBodyBehavior
    {
        [ItemGroupAssociation(QualityItemBehaviorUsageFlags.Server)]
        private static ItemQualityGroup GetItemGroup()
        {
            return ItemQualitiesContent.ItemQualityGroups.HealOnCrit;
        }

        public static GameObject healOnCritRangeIndicatorPrefab;

        private float _accumulatedHealing;
        private GameObject _healOnCritRangeIndicator;

        [ContentInitializer]
        private static IEnumerator LoadContent(ContentInitializerArgs args)
        {
            AsyncOperationHandle<GameObject> nearbyDamageBonusIndicatorLoad = AddressableUtil.LoadTempAssetAsync<GameObject>(RoR2_Base_NearbyDamageBonus.NearbyDamageBonusIndicator_prefab);
            AsyncOperationHandle<GameObject> halcBodyLoad = AddressableUtil.LoadAssetAsync<GameObject>(RoR2_DLC2_Halcyonite.HalcyoniteBody_prefab);

            ParallelProgressCoroutine coroutine = new ParallelProgressCoroutine(args.ProgressReceiver);
            coroutine.Add(nearbyDamageBonusIndicatorLoad);
            coroutine.Add(halcBodyLoad);

            yield return coroutine;

            if (nearbyDamageBonusIndicatorLoad.Status != AsyncOperationStatus.Succeeded || !nearbyDamageBonusIndicatorLoad.Result)
            {
                Log.Error($"Failed to load nearby damage bonus indicator prefab: {nearbyDamageBonusIndicatorLoad.OperationException}");
                yield break;
            }
            if (halcBodyLoad.Status != AsyncOperationStatus.Succeeded || !halcBodyLoad.Result)
            {
                Log.Error($"Failed to load halc body prefab: {halcBodyLoad.OperationException}");
                yield break;
            }

            healOnCritRangeIndicatorPrefab = nearbyDamageBonusIndicatorLoad.Result.InstantiateClone("healOnCritRangeIndicator", true);

            Dictionary<Material, Material> tintMaterialCache = new Dictionary<Material, Material>();
            foreach (Renderer renderer in healOnCritRangeIndicatorPrefab.GetComponentsInChildren<Renderer>(true))
            {
                if (!tintMaterialCache.TryGetValue(renderer.sharedMaterial, out Material tintMaterial))
                {
                    tintMaterial = new Material(renderer.sharedMaterial);
                    tintMaterial.SetColor(ShaderProperties._TintColor, new Color(0, 1, 0, 0.3f));

                    tintMaterialCache.Add(renderer.sharedMaterial, tintMaterial);
                }

                renderer.sharedMaterial = tintMaterial;
            }

            Bank halcBank = null;
            if (halcBodyLoad.Result.TryGetComponent(out AkBank halcBodyBank))
            {
                halcBank = halcBodyBank.data;
            }
            if (halcBank != null)
            {
                AkBank swingBank = healOnCritRangeIndicatorPrefab.AddComponent<AkBank>();
                swingBank.data = halcBank;
                swingBank.triggerList = new List<int> { AkTriggerHandler.ON_ENABLE_TRIGGER_ID };
                swingBank.unloadTriggerList = new List<int> { AkTriggerHandler.ON_DISABLE_TRIGGER_ID };
            }
            else
            {
                Log.Warning("Failed to load halcyonite sound bank");
            }

            args.ContentPack.networkedObjectPrefabs.Add(healOnCritRangeIndicatorPrefab);
        }

        private void OnEnable()
        {
            _healOnCritRangeIndicator = Instantiate(healOnCritRangeIndicatorPrefab, Body.corePosition, Quaternion.identity);
            _healOnCritRangeIndicator.GetComponent<NetworkedBodyAttachment>().AttachToGameObjectAndSpawn(gameObject);

            HealthComponent.onCharacterHealServer += onCharacterHealServer;
        }
       

        private void OnDisable()
        {
            Destroy(_healOnCritRangeIndicator);
            _healOnCritRangeIndicator = null;
            HealthComponent.onCharacterHealServer -= onCharacterHealServer;
        }


        private void onCharacterHealServer(HealthComponent healthComponent, float amount, ProcChainMask procChainMask)
        {
            if (!healthComponent || healthComponent != Body.healthComponent)
                return;

            _accumulatedHealing += amount;
            updateAccumulatedHealing();
        }

        protected override void OnStacksChanged()
        {
            base.OnStacksChanged();

            updateAccumulatedHealing();
        }

        private void updateAccumulatedHealing()
        {
            ref readonly ItemQualityCounts healOnCrit = ref Stacks;
            if (healOnCrit.TotalQualityCount == 0)
                return;
            if (Body.HasBuff(ItemQualitiesContent.Buffs.HealCritBoost))
                return;

            float healingThresholdFraction;
            switch (healOnCrit.HighestQuality)
            {
                case QualityTier.Uncommon:
                    healingThresholdFraction = 2f;
                    break;
                case QualityTier.Rare:
                    healingThresholdFraction = 1f;
                    break;
                case QualityTier.Epic:
                    healingThresholdFraction = 0.5f;
                    break;
                case QualityTier.Legendary:
                    healingThresholdFraction = 0.2f;
                    break;
                default:
                    Log.Error($"Quality tier {healOnCrit.HighestQuality} is not implemented");
                    healingThresholdFraction = 0f;
                    break;
            }

            float healingThreshold = healingThresholdFraction * Body.healthComponent.fullHealth;

            if (healingThreshold > 0 && _accumulatedHealing >= healingThreshold)
            {
                _accumulatedHealing = 0;
                Body.AddBuff(ItemQualitiesContent.Buffs.HealCritBoost);
            }
        }
    }
}
