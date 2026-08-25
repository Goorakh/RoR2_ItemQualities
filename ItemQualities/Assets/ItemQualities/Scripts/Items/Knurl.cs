using ItemQualities.ContentManagement;
using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using R2API;
using RoR2;
using RoR2BepInExPack.GameAssetPathsBetter;
using System.Collections;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace ItemQualities.Items
{
    internal static class Knurl
    {
        private static GameObject _explosionEffectPrefab;

        [ContentInitializer]
        private static IEnumerator LoadContent(ContentInitializerArgs args)
        {
            AsyncOperationHandle<GameObject> parentSlamEffectLoad = AddressableUtil.LoadTempAssetAsync<GameObject>(RoR2_Base_Parent.ParentSlamEffect_prefab);
            parentSlamEffectLoad.OnSuccess(parentSlamEffectPrefab =>
            {
                _explosionEffectPrefab = EffectScalingFixer.CreateFixedScalingCopy(parentSlamEffectPrefab, 10f, "KnurlExplosion");

                args.ContentPack.effectDefs.Add(new EffectDef(_explosionEffectPrefab));
            });

            return parentSlamEffectLoad.AsProgressCoroutine(args.ProgressReceiver);
        }

        [SystemInitializer]
        private static void Init()
        {
            On.RoR2.GlobalEventManager.ProcessHitEnemy += GlobalEventManager_ProcessHitEnemy;
        }

        private static void GlobalEventManager_ProcessHitEnemy(On.RoR2.GlobalEventManager.orig_ProcessHitEnemy orig, GlobalEventManager self, DamageInfo damageInfo, GameObject victim)
        {
            orig(self, damageInfo, victim);

            if (damageInfo.procCoefficient <= 0f)
                return;

            if (!damageInfo.attacker || !damageInfo.attacker.TryGetComponent(out CharacterBody attackerBody) || !attackerBody.inventory)
                return;

            BuffQualityCounts knurlReadyBuffCount = attackerBody.GetBuffCounts(ItemQualitiesContent.BuffQualityGroups.KnurlReady);
            if (knurlReadyBuffCount.TotalQualityCount == 0)
                return;

            ItemQualityCounts knurl = attackerBody.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.Knurl);
            if (knurl.TotalQualityCount == 0)
                return;

            if (damageInfo.damage >= attackerBody.damage * 6f && !damageInfo.procChainMask.HasModdedProc(ProcTypes.Knurl))
            {
                attackerBody.RemoveAllQualityBuffs(ItemQualitiesContent.BuffQualityGroups.KnurlReady);

                int cooldown;
                float blastRadius;

                QualityTier qualityTier = knurl.HighestQuality;
                switch (qualityTier)
                {
                    case QualityTier.Uncommon:
                        cooldown = 20;
                        blastRadius = 10f;
                        break;
                    case QualityTier.Rare:
                        cooldown = 16;
                        blastRadius = 20f;
                        break;
                    case QualityTier.Epic:
                        cooldown = 10;
                        blastRadius = 30f;
                        break;
                    case QualityTier.Legendary:
                        cooldown = 8;
                        blastRadius = 40f;
                        break;
                    default:
                        cooldown = 14;
                        blastRadius = 10f;
                        Log.Warning($"Quality tier {qualityTier} is not implemeted");
                        break;
                }

                if (damageInfo.crit)
                {
                    cooldown /= 2;
                }

                BuffIndex cooldownBuffIndex = ItemQualitiesContent.BuffQualityGroups.KnurlCooldown.GetBuffIndex(qualityTier);
                for (int i = 1; i <= cooldown; i++)
                {
                    attackerBody.AddTimedBuff(cooldownBuffIndex, i);
                }

                float damageCoefficient = (knurl.UncommonCount * 4f) +
                                          (knurl.RareCount * 5f) +
                                          (knurl.EpicCount * 6f) +
                                          (knurl.LegendaryCount * 8f);

                ProcChainMask procChainMask = damageInfo.procChainMask;
                procChainMask.AddModdedProc(ProcTypes.Knurl);

                Vector3 blastPosition = damageInfo.position;

                // This is preloaded by the item behavior, so no asset loading should actually need to be done here
                GameObject delayBlastPrefab = AddressableUtil.LoadAssetAsync<GameObject>(RoR2_Base_Common.GenericDelayBlast_prefab).WaitForCompletion();
                GameObject delayBlastObj = GameObject.Instantiate(delayBlastPrefab, blastPosition, Quaternion.identity);
                delayBlastObj.transform.localScale = new Vector3(blastRadius, blastRadius, blastRadius);

                DelayBlast delayBlast = delayBlastObj.GetComponent<DelayBlast>();
                delayBlast.position = blastPosition;
                delayBlast.radius = blastRadius;
                delayBlast.attacker = damageInfo.attacker;
                delayBlast.teamFilter.teamIndex = attackerBody.teamComponent.teamIndex;
                delayBlast.inflictor = delayBlastObj;
                delayBlast.falloffModel = BlastAttack.FalloffModel.None;
                delayBlast.baseDamage = damageCoefficient * damageInfo.damage;
                delayBlast.crit = damageInfo.crit;
                delayBlast.damageType = DamageType.Stun1s;
                delayBlast.damageColorIndex = DamageColorIndex.Item;
                delayBlast.procChainMask = procChainMask;
                delayBlast.procCoefficient = 0f;
                delayBlast.baseForce = 500f;
                delayBlast.explosionEffect = _explosionEffectPrefab;
                delayBlast.maxTimer = 0.2f;
            }
        }
    }

    public sealed class KnurlQualityItemBehavior : QualityItemBodyBehavior
    {
        [ItemGroupAssociation(QualityItemBehaviorUsageFlags.Server)]
        private static ItemQualityGroup GetItemGroup() => ItemQualitiesContent.ItemQualityGroups.Knurl;

        private static readonly AssetReferenceGameObject _genericDelayBlastPrefabReference = new AssetReferenceGameObject(RoR2_Base_Common.GenericDelayBlast_prefab);

        private void OnEnable()
        {
            AddressableUtil.LoadAssetAsync(_genericDelayBlastPrefabReference);
        }

        private void OnDisable()
        {
            AddressableUtil.UnloadAsset(_genericDelayBlastPrefabReference);

            Body.RemoveAllQualityBuffs(ItemQualitiesContent.BuffQualityGroups.KnurlReady);
            Body.RemoveAllQualityBuffs(ItemQualitiesContent.BuffQualityGroups.KnurlCooldown);
        }

        protected override void OnStacksChanged()
        {
            base.OnStacksChanged();
            Body.ConvertQualityBuffsToTier(ItemQualitiesContent.BuffQualityGroups.KnurlReady, Stacks.HighestQuality);
            Body.ConvertQualityBuffsToTier(ItemQualitiesContent.BuffQualityGroups.KnurlCooldown, Stacks.HighestQuality);
        }

        private void FixedUpdate()
        {
            if (Body.GetBuffCounts(ItemQualitiesContent.BuffQualityGroups.KnurlCooldown).TotalQualityCount == 0 &&
                Body.GetBuffCounts(ItemQualitiesContent.BuffQualityGroups.KnurlReady).TotalQualityCount == 0)
            {
                Body.AddBuff(ItemQualitiesContent.BuffQualityGroups.KnurlReady.GetBuffIndex(Stacks.HighestQuality));
            }
        }
    }
}
