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
using UnityEngine.Networking;
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

        private static int instanceCount;

        public static GameObject healOnCritRangeIndicatorPrefab;

        private float _accumulatedHealing;
        private GameObject _healOnCritRangeIndicator;

        [ContentInitializer]
        private static IEnumerator LoadContent(ContentInitializerArgs args)
        {
            AsyncOperationHandle<GameObject> nearbyDamageBonusIndicatorLoad = AddressableUtil.LoadTempAssetAsync<GameObject>(RoR2_Base_NearbyDamageBonus.NearbyDamageBonusIndicator_prefab);
            AsyncOperationHandle<WwiseBankReference> halcBankLoad = AddressableUtil.LoadAssetAsync<WwiseBankReference>(Wwise._88E33265_EA45_4136_A0CE_C7CC9EDA35BE_asset); // Mob_halcyonite

            ParallelProgressCoroutine coroutine = new ParallelProgressCoroutine(args.ProgressReceiver);
            coroutine.Add(nearbyDamageBonusIndicatorLoad);
            coroutine.Add(halcBankLoad);

            yield return coroutine;

            if (nearbyDamageBonusIndicatorLoad.AssertLoaded())
            {
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

                if (halcBankLoad.AssertLoaded())
                {
                    AkBank swingBank = healOnCritRangeIndicatorPrefab.AddComponent<AkBank>();
                    swingBank.data.WwiseObjectReference = halcBankLoad.Result;
                    swingBank.triggerList = new List<int> { AkTriggerHandler.ON_ENABLE_TRIGGER_ID };
                    swingBank.unloadTriggerList = new List<int> { AkTriggerHandler.ON_DISABLE_TRIGGER_ID };
                }

                args.ContentPack.networkedObjectPrefabs.Add(healOnCritRangeIndicatorPrefab);
            }
        }

        private void OnEnable()
        {
            _healOnCritRangeIndicator = Instantiate(healOnCritRangeIndicatorPrefab, Body.corePosition, Quaternion.identity);
            _healOnCritRangeIndicator.GetComponent<NetworkedBodyAttachment>().AttachToGameObjectAndSpawn(gameObject);

            HealthComponent.onCharacterHealServer += onCharacterHealServer;

            if (++instanceCount == 1)
            {
                GlobalEventManager.onServerDamageDealt += onServerDamageDealt;
            }
        }

        private void OnDisable()
        {
            HealthComponent.onCharacterHealServer -= onCharacterHealServer;

            if (--instanceCount == 0)
            {
                GlobalEventManager.onServerDamageDealt -= onServerDamageDealt;
            }

            Destroy(_healOnCritRangeIndicator);
            _healOnCritRangeIndicator = null;
        }

        private void onCharacterHealServer(HealthComponent healthComponent, float amount, ProcChainMask procChainMask)
        {
            if (!healthComponent || !ReferenceEquals(healthComponent, Body.healthComponent))
            {
                return;
            }

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

        private static void onServerDamageDealt(DamageReport report)
        {
            if (report == null || !report.attackerBody)
                return;

            const float minDistanceSqr = 13 * 13;

            if (report.attackerBody.HasBuff(ItemQualitiesContent.Buffs.HealCritBoost) &&
                (report.damageInfo.damageType.damageSource & DamageSource.Primary) != 0 &&
                (report.attackerBody.corePosition - report.damageInfo.position).sqrMagnitude <= minDistanceSqr)
            {
                report.attackerBody.RemoveBuff(ItemQualitiesContent.Buffs.HealCritBoost);

                InputBankTest inputBank = report.attackerBody.inputBank;
                Vector3 lookDirection = inputBank ? inputBank.aimDirection : report.attackerBody.transform.forward;
                lookDirection.y = 0f;
                lookDirection.Normalize();

                GameObject scytheEffect = Instantiate(ItemQualitiesContent.NetworkedPrefabs.ScytheEffect, report.attackerBody.corePosition, Util.QuaternionSafeLookRotation(lookDirection, Vector3.up));

                GenericOwnership genericOwnership = scytheEffect.GetComponent<GenericOwnership>();
                genericOwnership.ownerObject = report.attackerBody.gameObject;

                NetworkServer.Spawn(scytheEffect);
            }
        }
    }
}
