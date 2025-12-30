using HG;
using HG.Coroutines;
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
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace ItemQualities.Items
{
    static class OutOfCombatArmor
    {
        static readonly SphereSearch _opalSphereSearch = new SphereSearch();

        static GameObject _explosionVFX;

        [SystemInitializer]
        static void Init()
        {
            IL.RoR2.CharacterBody.OnTakeDamageServer += CharacterBody_OnTakeDamageServer;
        }

        [ContentInitializer]
        static IEnumerator LoadContent(ContentIntializerArgs args)
        {
            AsyncOperationHandle<GameObject> igniteOnKillExplosionLoad = AddressableUtil.LoadTempAssetAsync<GameObject>(RoR2_Base_IgniteOnKill.IgniteExplosionVFX_prefab);

            ParallelProgressCoroutine prefabsLoadCoroutine = new ParallelProgressCoroutine(args.ProgressReceiver);
            prefabsLoadCoroutine.Add(igniteOnKillExplosionLoad);

            yield return prefabsLoadCoroutine;

            if (igniteOnKillExplosionLoad.Status != AsyncOperationStatus.Succeeded || !igniteOnKillExplosionLoad.Result)
            {
                Log.Error($"Failed to load igniteOnKill VFX prefab: {igniteOnKillExplosionLoad.OperationException}");
                yield break;
            }

            _explosionVFX = igniteOnKillExplosionLoad.Result.InstantiateClone("opalExplosionVFX", false);
            UnityEngine.Object.Destroy(_explosionVFX.transform.Find("Flames").gameObject);
            UnityEngine.Object.Destroy(_explosionVFX.transform.Find("Flash").gameObject);
            args.ContentPack.effectDefs.Add(new EffectDef(_explosionVFX));
        }

        static void CharacterBody_OnTakeDamageServer(ILContext il)
        {
            if (!il.Method.TryFindParameter<DamageReport>(out ParameterDefinition damageReportParameter))
            {
                Log.Error("Failed to find DamageReport parameter");
                return;
            }

            ILCursor c = new ILCursor(il);

            if (!c.TryGotoNext(MoveType.After,
                               x => x.MatchStfld<CharacterBody>(nameof(CharacterBody.outOfDangerStopwatch))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Emit(OpCodes.Ldarg_0);
            c.Emit(OpCodes.Ldarg, damageReportParameter);
            c.EmitDelegate<Action<CharacterBody, DamageReport>>(onEnterDanger);

            static void onEnterDanger(CharacterBody victimBody, DamageReport damageReport)
            {
                if (!victimBody || damageReport?.damageInfo == null)
                    return;

                ItemQualityCounts outOfCombatArmor = ItemQualitiesContent.ItemQualityGroups.OutOfCombatArmor.GetItemCountsEffective(victimBody.inventory);
                if (outOfCombatArmor.TotalQualityCount <= 0)
                    return;

                float radius = 0;
                switch (outOfCombatArmor.HighestQuality)
                {
                    case QualityTier.Uncommon:
                        radius = 10;
                        break;
                    case QualityTier.Rare:
                        radius = 20;
                        break;
                    case QualityTier.Epic:
                        radius = 30;
                        break;
                    case QualityTier.Legendary:
                        radius = 50;
                        break;
                }

                float stunDuration = (outOfCombatArmor.UncommonCount * 2) +
                                     (outOfCombatArmor.RareCount * 4) +
                                     (outOfCombatArmor.EpicCount * 6) +
                                     (outOfCombatArmor.LegendaryCount * 8);

                if (victimBody.HasBuff(DLC1Content.Buffs.OutOfCombatArmorBuff))
                {
                    using var _ = ListPool<HurtBox>.RentCollection(out List<HurtBox> hurtBoxes);

                    _opalSphereSearch.origin = victimBody.corePosition;
                    _opalSphereSearch.mask = LayerIndex.entityPrecise.mask;
                    _opalSphereSearch.radius = ExplodeOnDeath.GetExplosionRadius(radius, victimBody);
                    _opalSphereSearch.RefreshCandidates();
                    _opalSphereSearch.FilterCandidatesByHurtBoxTeam(TeamMask.GetUnprotectedTeams(victimBody.teamComponent.teamIndex));
                    _opalSphereSearch.FilterCandidatesByDistinctHurtBoxEntities();
                    _opalSphereSearch.GetHurtBoxes(hurtBoxes);
                    _opalSphereSearch.ClearCandidates();

                    foreach (HurtBox hurtBox in hurtBoxes)
                    {
                        if (hurtBox.healthComponent && hurtBox.healthComponent.body != victimBody)
                        {
                            if (hurtBox.healthComponent.TryGetComponent(out SetStateOnHurt attackerSetStateOnHurt) && attackerSetStateOnHurt.canBeStunned)
                            {
                                attackerSetStateOnHurt.SetStun(stunDuration);
                                Crowbar.handleDelayedHit(victimBody.gameObject, hurtBox.gameObject);
                            }
                        }
                    }

                    EffectManager.SpawnEffect(_explosionVFX, new EffectData
                    {
                        origin = _opalSphereSearch.origin,
                        scale = _opalSphereSearch.radius,
                        rotation = Util.QuaternionSafeLookRotation(damageReport.damageInfo.force)
                    }, true);
                }
            }
        }
    }
}
