using ItemQualities.ContentManagement;
using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using R2API;
using RoR2;
using RoR2BepInExPack.GameAssetPathsBetter;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace ItemQualities.Items
{
    internal static class DeathMark
    {
        private static DamageColorIndex _soulStrikeColor;
        private static DamageColorIndex _soulStrikeCritColor;
        private static GameObject _soulStrikeEffect;

        [ContentInitializer]
        private static IEnumerator LoadContent(ContentInitializerArgs args)
        {
            AsyncOperationHandle<GameObject> voidExecuteLoad = AddressableUtil.LoadTempAssetAsync<GameObject>(RoR2_DLC1_CritGlassesVoid.CritGlassesVoidExecuteEffect_prefab);
            voidExecuteLoad.OnSuccess(voidExecute =>
            {
                _soulStrikeEffect = voidExecute.InstantiateClone("SoulStrikeEffect", false);

                Transform fakeDamageNumbers = _soulStrikeEffect.transform.Find("FakeDamageNumbers");
                if (fakeDamageNumbers)
                {
                    GameObject.Destroy(fakeDamageNumbers.gameObject);
                }
                foreach (ParticleSystem particleSystem in _soulStrikeEffect.transform.GetComponentsInChildren<ParticleSystem>())
                {
                    ParticleSystem.MainModule main = particleSystem.main;
                    main.startColor = Color.white;
                    main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                }

                Transform pointLight = _soulStrikeEffect.transform.Find("Point Light");
                if (pointLight && pointLight.TryGetComponent(out Light light))
                {
                    light.color = Color.white;
                }

                if (_soulStrikeEffect.TryGetComponent(out EffectComponent effectComponent))
                {
                    effectComponent.soundName = "Play_railgunner_shift_explo";
                }

                ChangeMaterial(_soulStrikeEffect.transform.Find("Small Stars"), args.ContentPack.materials.Find("matSoulStrikeStarParticleOffset"));
                ChangeMaterial(_soulStrikeEffect.transform.Find("Shards"), args.ContentPack.materials.Find("matSoulStrikeStarParticle"));
                ChangeMaterial(_soulStrikeEffect.transform.Find("Ring"), args.ContentPack.materials.Find("matSoulStrikeStarTrail"));
                ChangeMaterial(_soulStrikeEffect.transform.GetChild(4), args.ContentPack.materials.Find("matOmniHitspark2SoulStrike"));
                ChangeMaterial(_soulStrikeEffect.transform.GetChild(5), args.ContentPack.materials.Find("matOmniHitsparkSoulStrike"));
                ChangeMaterial(_soulStrikeEffect.transform.Find("OmniSparks"), args.ContentPack.materials.Find("matOmniHitsparkSoulStrike"));

                void ChangeMaterial(Transform child, Material newMaterial)
                {
                    if (child && child.TryGetComponent(out ParticleSystemRenderer particleSystemRenderer))
                    {
                        particleSystemRenderer.sharedMaterial = newMaterial;
                    }
                }

                args.ContentPack.effectDefs.Add(new EffectDef(_soulStrikeEffect));
            });

            return voidExecuteLoad.AsProgressCoroutine(args.ProgressReceiver);
        }

        [SystemInitializer]
        private static void Init()
        {
            _soulStrikeColor = ColorsAPI.RegisterDamageColor(new Color(0f, 0.4f, 0f));
            _soulStrikeCritColor = ColorsAPI.RegisterDamageColor(new Color(0f, 0.8f, 0f));

            IL.RoR2.HealthComponent.TakeDamageProcess += IL_HealthComponent_TakeDamageProcess;
            GlobalEventManager.onServerCharacterExecuted += OnServerCharacterExecuted;
        }

        private static void OnServerCharacterExecuted(DamageReport report, float damage)
        {
            if (report == null || !report.attackerBody || !report.victimBody)
                return;
            ItemQualityCounts deathMark = report.attackerBody.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.DeathMark);
            if (deathMark.TotalQualityCount == 0)
                return;

            int maxBuffGain = 100 - report.attackerBody.GetBuffCount(ItemQualitiesContent.Buffs.DeathMarkSouls);
            for (int i = 0; i < Math.Min((int)(damage / report.victimBody.maxHealth * 50), maxBuffGain); i++)
            {
                report.attackerBody.AddBuff(ItemQualitiesContent.Buffs.DeathMarkSouls);
            }
        }

        private static void IL_HealthComponent_TakeDamageProcess(ILContext il)
        {
            ILCursor c = new ILCursor(il);
            int damageLoc = 0;
            ILLabel label = null;

            if (!il.Method.TryFindParameter<DamageInfo>(out ParameterDefinition damageInfoParameter))
            {
                Log.Error("Failed to find DamageInfo parameter");
                return;
            }

            if (!c.TryGotoNext(
                x => x.MatchLdfld(typeof(DamageInfo), nameof(DamageInfo.damage)),
                x => x.MatchStloc(out damageLoc)
            ))
            {
                Log.Error("Failed to find damage field location");
                return;
            }

            if (!c.TryGotoNext(
                x => x.MatchLdsfld(typeof(RoR2Content.Buffs), nameof(RoR2Content.Buffs.DeathMark)),
                x => x.MatchCallOrCallvirt(typeof(CharacterBody), nameof(CharacterBody.HasBuff)),
                x => x.MatchBrfalse(out label)
            ))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.GotoLabel(label);
            c.Emit(OpCodes.Ldarg_0);
            c.Emit(OpCodes.Ldarg, damageInfoParameter);
            c.Emit(OpCodes.Ldloc, damageLoc);
            c.EmitDelegate<Func<HealthComponent, DamageInfo, float, float>>(RollSoulStrike);
            c.Emit(OpCodes.Stloc, damageLoc);
        }

        private static float RollSoulStrike(HealthComponent self, DamageInfo damageInfo, float damage)
        {
            if ((damageInfo.damageType.damageTypeExtended & DamageTypeExtended.BypassDamageCalculations) != 0)
                return damage;
            if (!damageInfo.attacker)
                return damage;
            if (!damageInfo.attacker.TryGetComponent(out CharacterBody attackerBody))
                return damage;
            if (!attackerBody.master)
                return damage;

            ItemQualityCounts deathMark = attackerBody.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.DeathMark);
            if (deathMark.TotalQualityCount == 0)
                return damage;

            float SoulStrikeChance = 5 + attackerBody.GetBuffCount(ItemQualitiesContent.Buffs.DeathMarkSouls);
            if (RollUtil.CheckRoll(SoulStrikeChance, attackerBody.master, damageInfo.procChainMask.HasProc(ProcType.SureProc)))
            {
                damageInfo.damageColorIndex = damageInfo.crit ? _soulStrikeCritColor : _soulStrikeColor;
                EffectManager.SpawnEffect(_soulStrikeEffect, new EffectData
                {
                    origin = self.body.corePosition,
                    scale = self.body.radius / 2
                }, transmit: true);

                int soulDrainMax = deathMark.HighestQuality switch
                {
                    QualityTier.Uncommon => 4,
                    QualityTier.Rare => 3,
                    QualityTier.Epic => 2,
                    QualityTier.Legendary => 1,
                    _ => 0
                };
                for (int soul = 0; soul < Mathf.Min(soulDrainMax, attackerBody.GetBuffCount(ItemQualitiesContent.Buffs.DeathMarkSouls)); soul++)
                {
                    attackerBody.RemoveBuff(ItemQualitiesContent.Buffs.DeathMarkSouls);
                }

                float stackDamageCoeff =(deathMark.UncommonCount * 1) +
                                        (deathMark.RareCount * 2) +
                                        (deathMark.EpicCount * 3) +
                                        (deathMark.LegendaryCount * 4);

                float baseLevelMaxHealth = attackerBody.baseMaxHealth + (attackerBody.levelMaxHealth * (attackerBody.level - 1));
                float damageCoeff = ((attackerBody.maxHealth - baseLevelMaxHealth) * 0.01f * stackDamageCoeff) + 0.5f;
                return damage * (damageCoeff + 1);
            }
            return damage;
        }
    }
}
