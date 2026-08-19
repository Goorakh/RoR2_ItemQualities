using ItemQualities.ContentManagement;
using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using R2API;
using RoR2;
using RoR2.Projectile;
using RoR2BepInExPack.GameAssetPathsBetter;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace ItemQualities.Items
{
    internal static class MinorConstructOnKill
    {
        [ContentInitializer]
        private static IEnumerator LoadContent(ContentInitializerArgs args)
        {
            AsyncOperationHandle<GameObject> goldSiphonTetherVFXLoad = AddressableUtil.LoadTempAssetAsync<GameObject>(RoR2_DLC2.GoldSiphonTetherVFX_prefab);
            goldSiphonTetherVFXLoad.OnSuccess(goldSiphonTetherVFX =>
            {
                GameObject constructBubbleTether = goldSiphonTetherVFX.InstantiateClone("ConstructBubbleTetherVFX", false);
                if (constructBubbleTether.TryGetComponent(out LineRenderer lineRenderer))
                {
                    lineRenderer.widthMultiplier = 0.1f;
                }
                else
                {
                    Log.Error($"{constructBubbleTether} is missing LineRenderer component");
                }

                GameObject attachmentPrefab = args.ContentPack.networkedObjectPrefabs.Find(nameof(ItemQualitiesContent.NetworkedPrefabs.QualityMinorConstructOnKillAttachment));
                if (!attachmentPrefab)
                {
                    Log.Error("Failed to find attachment prefab in content pack");
                    return;
                }

                attachmentPrefab.GetComponent<TetherVfxOrigin>().tetherPrefab = constructBubbleTether;
            });

            return goldSiphonTetherVFXLoad.AsProgressCoroutine(args.ProgressReceiver);
        }

        [SystemInitializer]
        private static void Init()
        {
            IL.RoR2.Projectile.ProjectileSpawnMaster.SpawnMaster += ProjectileSpawnMaster_SpawnMaster;
        }

        private static void ProjectileSpawnMaster_SpawnMaster(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            VariableDefinition spawnRequestVar = null;
            if (!c.TryGotoNext(MoveType.After,
                               x => x.MatchNewobj<DirectorSpawnRequest>(),
                               x => x.MatchStloc<DirectorSpawnRequest>(il, out spawnRequestVar)))
            {
                Log.PatchError(il, "Failed to find patch location");
                return;
            }

            c.Emit(OpCodes.Ldarg_0);
            c.Emit(OpCodes.Ldloc, spawnRequestVar);
            c.EmitDelegate<Action<ProjectileSpawnMaster, DirectorSpawnRequest>>(setupSpawnRequest);

            static void setupSpawnRequest(ProjectileSpawnMaster projectileSpawnMaster, DirectorSpawnRequest directorSpawnRequest)
            {
                if (projectileSpawnMaster &&
                    projectileSpawnMaster.TryGetComponent(out ProjectileController projectileController) &&
                    projectileController.owner &&
                    projectileController.owner.TryGetComponent(out CharacterBody ownerBody) &&
                    ownerBody.inventory)
                {
                    ItemQualityCounts minorConstructOnKill = ownerBody.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.MinorConstructOnKill);
                    minorConstructOnKill.BaseItemCount = 0;

                    if (minorConstructOnKill.TotalQualityCount > 0)
                    {
                        directorSpawnRequest.onSpawnedServer += onSpawnedServer;

                        void onSpawnedServer(SpawnCard.SpawnResult spawnResult)
                        {
                            if (spawnResult.success &&
                                spawnResult.spawnedInstance &&
                                spawnResult.spawnedInstance.TryGetComponent(out CharacterMaster spawnedMaster))
                            {
                                spawnedMaster.inventory.GiveItemsPermanent(ItemQualitiesContent.ItemQualityGroups.MinorConstructOnKillConstructItem, minorConstructOnKill);
                                spawnedMaster.inventory.GiveItemPermanent(ItemQualitiesContent.ItemQualityGroups.QualityTier.GetItemIndex(minorConstructOnKill.HighestQuality));
                            }
                        }
                    }
                }
            }
        }
    }

    public sealed class MinorConstructOnKillConstructItemQualityItemBehavior : QualityItemBodyBehavior
    {
        [ItemGroupAssociation(QualityItemBehaviorUsageFlags.Server)]
        private static ItemQualityGroup GetItemGroup() => ItemQualitiesContent.ItemQualityGroups.MinorConstructOnKillConstructItem;

        private GameObject _attachmentInstance;

        private void OnEnable()
        {
            _attachmentInstance = Instantiate(ItemQualitiesContent.NetworkedPrefabs.QualityMinorConstructOnKillAttachment);
            _attachmentInstance.GetComponent<NetworkedBodyAttachment>().AttachToGameObjectAndSpawn(gameObject);
        }

        private void OnDisable()
        {
            Destroy(_attachmentInstance);
            _attachmentInstance = null;
        }
    }
}
