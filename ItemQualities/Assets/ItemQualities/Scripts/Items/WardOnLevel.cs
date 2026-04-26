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
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace ItemQualities.Items
{
    static class WardOnLevel
    {
        static GameObject _wardTemporaryPrefab;

        [ContentInitializer]
        static IEnumerator LoadContent(ContentInitializerArgs args)
        {
            AsyncOperationHandle<GameObject> warbannerWardLoad = AddressableUtil.LoadTempAssetAsync<GameObject>(RoR2_Base_WardOnLevel.WarbannerWard_prefab);
            warbannerWardLoad.OnSuccess(warbannerWard =>
            {
                warbannerWard.AddComponent<WardOnLevelGrowingBuff>().enabled = false;
                GameObject warbannerWardTemporaryObj = warbannerWard.InstantiateClone("WarbannerWardTemporary");

                GenericDurationComponent durationComponent = warbannerWardTemporaryObj.AddComponent<GenericDurationComponent>();

                BuffWard buffWard = warbannerWardTemporaryObj.GetComponent<BuffWard>();
                buffWard.expires = true;
                buffWard.expireDuration = 15f;

                durationComponent.BuffWard = buffWard;

                Transform modelTransform = warbannerWardTemporaryObj.transform.Find("mdlWarbanner");
                if (modelTransform)
                {
                    GameObject modelRootObj = new GameObject("ModelRoot");
                    modelRootObj.transform.SetParent(modelTransform, false);
                    modelRootObj.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                    modelRootObj.transform.localScale = Vector3.one;

                    for (int i = modelTransform.childCount - 1; i >= 0; i--)
                    {
                        Transform modelChild = modelTransform.GetChild(i);
                        if (modelChild != modelRootObj.transform)
                        {
                            modelChild.SetParent(modelRootObj.transform, true);
                        }
                    }

                    BeginRapidlyActivatingAndDeactivating endBlinkController = warbannerWardTemporaryObj.AddComponent<BeginRapidlyActivatingAndDeactivating>();
                    endBlinkController.delayBeforeBeginningBlinking = 9f;
                    endBlinkController.blinkFrequency = 10f;
                    endBlinkController.blinkingRootObject = modelRootObj;

                    durationComponent.BlinkController = endBlinkController;
                    durationComponent.BlinkDuration = 5f;
                }
                else
                {
                    Log.Error($"Failed to find warbanner model root on {Util.GetGameObjectHierarchyName(warbannerWardTemporaryObj)}");
                }

                args.ContentPack.networkedObjectPrefabs.Add(warbannerWardTemporaryObj);
                _wardTemporaryPrefab = warbannerWardTemporaryObj;
            });

            return warbannerWardLoad.AsProgressCoroutine(args.ProgressReceiver);
        }

        [SystemInitializer]
        static void Init()
        {
            GlobalEventManager.OnInteractionsGlobal += onInteractionsGlobal;
            IL.RoR2.Items.WardOnLevelManager.OnCharacterLevelUp += WardOnLevelManager_OnCharacterLevelUp;
            IL.RoR2.TeleporterInteraction.ChargingState.OnEnter += ChargingState_OnEnter;
            RecalculateStatsAPI.GetStatCoefficients += getStatCoefficients;
        }

        private static void getStatCoefficients(CharacterBody sender, RecalculateStatsAPI.StatHookEventArgs args)
        {
            if (!sender)
                return;

            BuffQualityCounts warbanner = sender.GetBuffCounts(ItemQualitiesContent.BuffQualityGroups.Warbanner);
            args.attackSpeedMultAdd +=  warbanner.UncommonCount * 0.02f +
                                        warbanner.RareCount * 0.03f +
                                        warbanner.EpicCount * 0.04f +
                                        warbanner.LegendaryCount * 0.05f;
        }

        private static void WardOnLevelManager_OnCharacterLevelUp(ILContext il)
        {
            ILCursor c = new ILCursor(il);
            if (c.TryGotoNext(MoveType.After,
                    x => x.MatchCallOrCallvirt(typeof(UnityEngine.Object), nameof(UnityEngine.Object.Instantiate))
                ))
            {
                c.Emit(OpCodes.Dup);
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate<Action<GameObject, CharacterBody>>(addGrowingBuff);
            }
            else
            {
                Log.Error(il.Method.Name + " IL Hook failed!");
                return;
            }
        }

        private static void ChargingState_OnEnter(ILContext il)
        {
            ILCursor c = new ILCursor(il);
            int body = 0;

            if (c.TryGotoNext(
                    x => x.MatchCallOrCallvirt(typeof(TeamComponent), "get_body"),
                    x => x.MatchStloc(out body)
            ) &&
            c.TryGotoNext(MoveType.After,
                    x => x.MatchCallOrCallvirt(typeof(UnityEngine.Object), nameof(UnityEngine.Object.Instantiate))
                ))
            {
                c.Emit(OpCodes.Dup);
                c.Emit(OpCodes.Ldloc, body);
                c.EmitDelegate<Action<GameObject, CharacterBody>>(addGrowingBuff);
            }
            else
            {
                Log.Error(il.Method.Name + " IL Hook failed!");
                return;
            }
        }

        static void addGrowingBuff(GameObject banner, CharacterBody body)
        {
            ItemQualityCounts WardOnLevel = body.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.WardOnLevel);
            if (WardOnLevel.TotalQualityCount > 0)
            {
                banner.GetComponent<BuffWard>().buffDef = null;
                WardOnLevelGrowingBuff wardOnLevelGrowingBuff = banner.GetComponent<WardOnLevelGrowingBuff>();
                wardOnLevelGrowingBuff.enabled = true;
                wardOnLevelGrowingBuff.buff = ItemQualitiesContent.BuffQualityGroups.Warbanner.GetBuffDef(WardOnLevel.HighestQuality);
                wardOnLevelGrowingBuff.maxStacks = WardOnLevel.UncommonCount * 30 +
                                                    WardOnLevel.RareCount * 40 +
                                                    WardOnLevel.EpicCount * 50 +
                                                    WardOnLevel.LegendaryCount * 60;
            }
        }

        static void onInteractionsGlobal(Interactor interactor, IInteractable interactable, GameObject interactableObject)
        {
            if (!NetworkServer.active)
                return;

            if (!SharedItemUtils.InteractableIsPermittedForSpawn(interactable))
                return;

            CharacterBody interactorBody = interactor ? interactor.GetComponent<CharacterBody>() : null;
            TeamIndex interactorTeam = interactorBody && interactorBody.teamComponent ? interactorBody.teamComponent.teamIndex : TeamIndex.None;

            Inventory interactorInventory = interactorBody ? interactorBody.inventory : null;
            if (!interactorInventory)
                return;

            ItemQualityCounts wardOnLevel = interactorInventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.WardOnLevel);

            if (wardOnLevel.TotalQualityCount > 0)
            {
                float wardDuration = (10f * wardOnLevel.UncommonCount) +
                                     (20f * wardOnLevel.RareCount) +
                                     (30f * wardOnLevel.EpicCount) +
                                     (40f * wardOnLevel.LegendaryCount);

                if (wardDuration > 0f)
                {
                    Vector3 wardSpawnPosition = interactorBody ? interactorBody.footPosition : interactableObject.transform.position;

                    GameObject temporaryWardObj = GameObject.Instantiate(_wardTemporaryPrefab, wardSpawnPosition, Quaternion.identity);

                    TeamFilter teamFilter = temporaryWardObj.GetComponent<TeamFilter>();
                    teamFilter.teamIndex = interactorTeam;

                    BuffWard buffWard = temporaryWardObj.GetComponent<BuffWard>();
                    buffWard.Networkradius = 8f + (8f * wardOnLevel.TotalCount);

                    GenericDurationComponent durationComponent = temporaryWardObj.GetComponent<GenericDurationComponent>();
                    durationComponent.Duration = wardDuration;
                    addGrowingBuff(temporaryWardObj, interactorBody);

                    NetworkServer.Spawn(temporaryWardObj);
                }
            }
        }
    }
    public class WardOnLevelGrowingBuff : NetworkBehaviour
    {
        float _buffTimer;
        TeamFilter _teamFilter;
        BuffWard _buffWard;

        public int maxStacks;
        public BuffDef buff;

        private void Awake()
        {
            _teamFilter = GetComponent<TeamFilter>();
            _buffWard = GetComponent<BuffWard>();
        }

        private void FixedUpdate()
        {
            if (!NetworkServer.active)
            {
                return;
            }

            _buffTimer -= Time.fixedDeltaTime;
            if (_buffTimer > 0f)
            {
                return;
            }
            _buffTimer = 1;
            
            BuffTeam(TeamComponent.GetTeamMembers(_teamFilter.teamIndex), _buffWard.radius * _buffWard.radius, base.transform.position);
        }

        private void BuffTeam(IEnumerable<TeamComponent> recipients, float radiusSqr, Vector3 currentPosition)
        {
            if (!NetworkServer.active)
            {
                return;
            }

            foreach (TeamComponent recipient in recipients)
            {
                Vector3 vector = recipient.transform.position - currentPosition;
                if (vector.sqrMagnitude > radiusSqr)
                    continue;

                CharacterBody characterBody = recipient.body;
                if (!characterBody)
                    continue;
                if (characterBody.healthComponent && characterBody.healthComponent.alive)
                {
                    characterBody.AddTimedBuff(buff, 1.5f, maxStacks);
                    characterBody.SetTimedBuffDurationIfPresent(buff, 1.5f, allStacks: true);
                }
            }
        }
    }
}
