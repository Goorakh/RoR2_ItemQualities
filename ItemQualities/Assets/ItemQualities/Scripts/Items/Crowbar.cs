using ItemQualities.ContentManagement;
using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using R2API;
using RoR2;
using RoR2BepInExPack.GameAssetPathsBetter;
using System.Collections;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace ItemQualities.Items
{
    internal static class Crowbar
    {
        private static GameObject _impactEffectPrefab;

        [ContentInitializer]
        private static IEnumerator LoadContent(ContentInitializerArgs args)
        {
            AsyncOperationHandle<GameObject> iceRingExplosionLoad = AddressableUtil.LoadTempAssetAsync<GameObject>(RoR2_Base_ElementalRings.IceRingExplosion_prefab);
            iceRingExplosionLoad.OnSuccess(iceRingExplosionPrefab =>
            {
                _impactEffectPrefab = iceRingExplosionPrefab.InstantiateClone("CrowbarExplosion", false);

                Transform iceMesh = _impactEffectPrefab.transform.Find("IceMesh");
                if (iceMesh && iceMesh.TryGetComponent(out ParticleSystemRenderer iceMeshRenderer))
                {
                    Material iceMeshMaterial = new Material(iceMeshRenderer.sharedMaterial);
                    iceMeshMaterial.color = new Color32(0xA8, 0xA1, 0x94, 0xFF);
                    iceMeshRenderer.sharedMaterial = iceMeshMaterial;
                }

                Transform chunks = _impactEffectPrefab.transform.Find("Chunks");
                if (chunks && chunks.TryGetComponent(out ParticleSystem chunksParticleSystem))
                {
                    ParticleSystem.MainModule mainModule = chunksParticleSystem.main;
                    mainModule.startColor = new ParticleSystem.MinMaxGradient(new Color32(0x89, 0x87, 0x84, 0xFF));
                }

                Transform billboardSplash = _impactEffectPrefab.transform.Find("BillboardSplash");
                if (billboardSplash)
                {
                    billboardSplash.gameObject.SetActive(false);
                }

                Transform runeRings = _impactEffectPrefab.transform.Find("RuneRings");
                if (runeRings)
                {
                    runeRings.gameObject.SetActive(false);
                }

                args.ContentPack.effectDefs.Add(new EffectDef(_impactEffectPrefab));
            });

            return iceRingExplosionLoad.AsProgressCoroutine(args.ProgressReceiver);
        }

        [SystemInitializer]
        private static void Init()
        {
            On.RoR2.GlobalEventManager.ProcessHitEnemy += GlobalEventManager_ProcessHitEnemy;
        }

        private static void GlobalEventManager_ProcessHitEnemy(On.RoR2.GlobalEventManager.orig_ProcessHitEnemy orig, GlobalEventManager self, DamageInfo damageInfo, GameObject victim)
        {
            orig(self, damageInfo, victim);

            if (!damageInfo.attacker || !damageInfo.attacker.TryGetComponent(out CharacterBody attackerBody) || !attackerBody.inventory)
                return;

            ItemQualityCounts crowbar = attackerBody.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.Crowbar);
            if (crowbar.TotalQualityCount == 0)
                return;

            if (!victim || !victim.TryGetComponent(out CharacterBody victimBody))
                return;

            if (damageInfo.damage >= attackerBody.damage * 4f && !damageInfo.procChainMask.HasModdedProc(ProcTypes.Crowbar))
            {
                BuffQualityCounts crowbarCharge = attackerBody.GetBuffCounts(ItemQualitiesContent.BuffQualityGroups.CrowbarCharge);
                if (crowbarCharge.TotalQualityCount >= 9)
                {
                    float damageCoefficient = (crowbar.UncommonCount * 0.5f) +
                                              (crowbar.RareCount * 1.0f) +
                                              (crowbar.EpicCount * 2.5f) +
                                              (crowbar.LegendaryCount * 4.0f);

                    ProcChainMask procChainMask = damageInfo.procChainMask;
                    procChainMask.AddModdedProc(ProcTypes.Crowbar);

                    DamageInfo crowbarDamageInfo = new DamageInfo
                    {
                        attacker = damageInfo.attacker,
                        damage = damageCoefficient * damageInfo.damage,
                        crit = damageInfo.crit,
                        procChainMask = procChainMask,
                        procCoefficient = 1f,
                        position = damageInfo.position,
                        damageColorIndex = DamageColorIndex.Item,
                        inflictedHurtbox = damageInfo.inflictedHurtbox,
                    };

                    victimBody.healthComponent.TakeDamage(crowbarDamageInfo);

                    EffectManager.SimpleEffect(_impactEffectPrefab, damageInfo.position, Quaternion.identity, true);

                    attackerBody.RemoveAllQualityBuffs(ItemQualitiesContent.BuffQualityGroups.CrowbarCharge);
                }
                else
                {
                    attackerBody.AddBuff(ItemQualitiesContent.BuffQualityGroups.CrowbarCharge.GetBuffIndex(crowbar.HighestQuality));
                }
            }
        }
    }

    public sealed class CrowbarQualityItemBehavior : QualityItemBodyBehavior
    {
        [ItemGroupAssociation(QualityItemBehaviorUsageFlags.Server)]
        private static ItemQualityGroup GetItemGroup() => ItemQualitiesContent.ItemQualityGroups.Crowbar;

        protected override void OnStacksChanged()
        {
            base.OnStacksChanged();
            Body.ConvertQualityBuffsToTier(ItemQualitiesContent.BuffQualityGroups.CrowbarCharge, Stacks.HighestQuality);
        }

        private void OnDisable()
        {
            Body.RemoveAllQualityBuffs(ItemQualitiesContent.BuffQualityGroups.CrowbarCharge);
        }
    }
}
