using EntityStates;
using HG.Coroutines;
using ItemQualities.ContentManagement;
using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
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
    static class Feather
    {
        static GameObject _featherEffectOut;
        static GameObject _featherEffectLast;

        [SystemInitializer]
        static void Init()
        {
            RecalculateStatsAPI.GetStatCoefficients += getStatCoefficients;
            IL.EntityStates.GenericCharacterMain.ProcessJump_bool += FeatherEffect;
        }

        [ContentInitializer]
        static IEnumerator LoadContent(ContentIntializerArgs args)
        {
            AsyncOperationHandle<GameObject> featherEffectLoad = AddressableUtil.LoadTempAssetAsync<GameObject>(RoR2_Base_Feather.FeatherEffect_prefab);

            ParallelProgressCoroutine prefabsLoadCoroutine = new ParallelProgressCoroutine(args.ProgressReceiver);
            prefabsLoadCoroutine.Add(featherEffectLoad);

            yield return prefabsLoadCoroutine;

            if (featherEffectLoad.Status != AsyncOperationStatus.Succeeded || !featherEffectLoad.Result)
            {
                Log.Error($"Failed to load feather effect prefab: {featherEffectLoad.OperationException}");
                yield break;
            }

            _featherEffectOut = featherEffectLoad.Result.InstantiateClone("featherEffectOut", false);
            change_color(_featherEffectOut.transform.Find("Big Feathers"), new Color(1, 0.9f, 0));
            change_color(_featherEffectOut.transform.Find("Ring"), new Color(1, 0.9f, 0));
            args.ContentPack.effectDefs.Add(new EffectDef(_featherEffectOut));

            _featherEffectLast = featherEffectLoad.Result.InstantiateClone("featherEffectLast", false);
            change_color(_featherEffectLast.transform.Find("Big Feathers"), new Color(1, 0.05f, 0));
            change_color(_featherEffectLast.transform.Find("Ring"), new Color(1, 0.05f, 0));
            args.ContentPack.effectDefs.Add(new EffectDef(_featherEffectLast));

            void change_color(Transform child, Color color)
            {
                if (!child)
                    return;

                ParticleSystemRenderer partsys = child.GetComponent<ParticleSystemRenderer>();
                if (!partsys)
                    return;

                partsys.material.SetColor("_TintColor", color);
                partsys.material.SetColor("_Color", color);
            }
        }

        private static void FeatherEffect(ILContext il)
        {
            ILCursor c = new ILCursor(il);
            if (!c.TryFindNext(out ILCursor[] foundCursors,
                    x => x.MatchLdstr("Prefabs/Effects/FeatherEffect"),
                    x => x.MatchCall(typeof(LegacyResourcesAPI), "Load")
                ))
            {
                Log.Error(il.Method.Name + " IL Hook failed!");
                return;
            }

            c.Goto(foundCursors[1].Next, MoveType.After);
            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<GameObject, GenericCharacterMain, GameObject>>(changeFeatherEffect);
        }

        static GameObject changeFeatherEffect(GameObject prefab, GenericCharacterMain self)
        {
            CharacterBody body = self?.characterBody;
            if (body || !body.inventory)
                return prefab;

            ItemQualityCounts feather = body.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.Feather);
            int maxJumps = (feather.UncommonCount * 3) +
                           (feather.RareCount * 5) +
                           (feather.EpicCount * 7) +
                           (feather.LegendaryCount * 9) +
                           feather.BaseItemCount +
                           body.baseJumpCount - 1;

            if (self.characterMotor.jumpCount == body.maxJumpCount - 1)
            {
                if (self.characterMotor.jumpCount == maxJumps)
                {
                    return _featherEffectLast;
                }
                else
                {
                    return _featherEffectOut;
                }
            }
            else
            {
                return prefab;
            }
        }

        static void getStatCoefficients(CharacterBody sender, RecalculateStatsAPI.StatHookEventArgs args)
        {
            if (!sender)
                return;

            args.jumpCountAdd += sender.GetBuffCount(ItemQualitiesContent.Buffs.FeatherExtraJumps);
        }
    }
}
