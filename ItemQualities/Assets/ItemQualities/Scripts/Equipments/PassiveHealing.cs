using HG;
using HG.Coroutines;
using ItemQualities.ContentManagement;
using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using R2API;
using RoR2;
using RoR2.CharacterAI;
using RoR2.Projectile;
using RoR2BepInExPack.GameAssetPathsBetter;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace ItemQualities.Equipments
{
    static class PassiveHealing
    {
        static GameObject _thrownObjectProjectileSilentPrefab;

        static GameObject _teleportEffectPrefab;

        static DeployableSlot _woodspriteCloneDeployableSlot = DeployableSlot.None;

        [InitDuringStartupPhase(GameInitPhase.PreFrame)]
        static void EarlyInit()
        {
            _woodspriteCloneDeployableSlot = DeployableAPI.RegisterDeployableSlot(getWoodspriteCloneLimit);
        }

        static int getWoodspriteCloneLimit(CharacterMaster self, int deployableCountMultiplier)
        {
            QualityTier passiveHealingQualityTier = QualityTier.None;
            int equipmentSlotCount = self.inventory.GetEquipmentSlotCount();
            for (uint slot = 0; slot < equipmentSlotCount; slot++)
            {
                int equipmentSetCount = self.inventory.GetEquipmentSetCount(slot);
                for (uint set = 0; set < equipmentSetCount; set++)
                {
                    passiveHealingQualityTier = QualityCatalog.Max(passiveHealingQualityTier, self.inventory.GetEquipmentQualityTier(slot, set));
                }
            }

            switch (passiveHealingQualityTier)
            {
                case QualityTier.None:
                case QualityTier.Uncommon:
                    return 1;
                case QualityTier.Rare:
                    return 2;
                case QualityTier.Epic:
                    return 3;
                case QualityTier.Legendary:
                    return 5;
                default:
                    Log.Warning($"Quality tier {passiveHealingQualityTier} is not implemented");
                    return 1;
            }
        }

        [ContentInitializer]
        static IEnumerator LoadContent(ContentInitializerArgs args)
        {
            ParallelProgressCoroutine coroutine = new ParallelProgressCoroutine(args.ProgressReceiver);

            AsyncOperationHandle<GameObject> thrownObjectProjectileLoad = AddressableUtil.LoadTempAssetAsync<GameObject>(RoR2_DLC3_Drifter.ThrownObjectProjectileNoStun_prefab);
            thrownObjectProjectileLoad.OnSuccess(thrownObjectProjectilePrefab =>
            {
                _thrownObjectProjectileSilentPrefab = thrownObjectProjectilePrefab.InstantiateClone("ThrownObjectProjectileSilent");

                RotateObject rotateObject = _thrownObjectProjectileSilentPrefab.GetComponentInChildren<RotateObject>();
                if (rotateObject)
                {
                    rotateObject.enabled = false;
                }

                Transform junkTrail = _thrownObjectProjectileSilentPrefab.transform.Find("JunkTrail");
                if (junkTrail)
                {
                    junkTrail.gameObject.SetActive(false);
                }

                args.ContentPack.projectilePrefabs.Add(_thrownObjectProjectileSilentPrefab);
            });

            coroutine.Add(thrownObjectProjectileLoad);

            AsyncOperationHandle<GameObject> teleportEffectLoad = AddressableUtil.LoadTempAssetAsync<GameObject>(RoR2_DLC1_BossHunter.BossHunterGunEffect_prefab);
            teleportEffectLoad.OnSuccess(teleportEffectPrefab =>
            {
                _teleportEffectPrefab = teleportEffectPrefab.InstantiateClone("PassiveHealingQualityTeleportEffect", false);

                Transform gunMeshTransform = _teleportEffectPrefab.transform.Find("Blast/GunMesh");
                if (gunMeshTransform)
                {
                    gunMeshTransform.gameObject.SetActive(false);
                }

                Transform shardsMeshTransform = _teleportEffectPrefab.transform.Find("Blast/Shards, Mesh");
                if (shardsMeshTransform)
                {
                    shardsMeshTransform.gameObject.SetActive(false);
                }

                Transform shardsLightTransform = _teleportEffectPrefab.transform.Find("Blast/Shards light");
                if (shardsLightTransform)
                {
                    shardsLightTransform.gameObject.SetActive(false);
                }

                Transform spookies2Transform = _teleportEffectPrefab.transform.Find("Blast/Spookies2");
                if (spookies2Transform)
                {
                    spookies2Transform.gameObject.SetActive(false);
                }

                Transform PPTransform = _teleportEffectPrefab.transform.Find("Blast/PP");
                if (PPTransform)
                {
                    PPTransform.gameObject.SetActive(false);
                }

                args.ContentPack.effectDefs.Add(new EffectDef(_teleportEffectPrefab));
            });

            coroutine.Add(teleportEffectLoad);

            return coroutine;
        }

        [SystemInitializer]
        static void Init()
        {
            IL.RoR2.EquipmentSlot.FirePassiveHealing += EquipmentSlot_FirePassiveHealing;
        }

        static void EquipmentSlot_FirePassiveHealing(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (!c.TryGotoNext(MoveType.After,
                               x => x.MatchCallOrCallvirt<HealingFollowerController>(nameof(HealingFollowerController.AssignNewTarget))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Action<EquipmentSlot>>(onAssignTarget);

            static void onAssignTarget(EquipmentSlot equipmentSlot)
            {
                if (!equipmentSlot || !equipmentSlot.passiveHealingFollower)
                    return;

                QualityTier qualityTier = equipmentSlot.GetCurrentEquipmentActionQualityTier();
                if (qualityTier == QualityTier.None)
                    return;

                GameObject targetBodyObject = equipmentSlot.passiveHealingFollower.targetBodyObject;
                CharacterBody targetBody = targetBodyObject ? targetBodyObject.GetComponent<CharacterBody>() : null;

                if (!targetBody || targetBody == equipmentSlot.characterBody)
                    return;

                MasterCatalog.MasterIndex cloneMasterIndex = MasterCatalog.FindAiMasterIndexForBody(targetBody.bodyIndex);
                GameObject cloneMasterPrefab = MasterCatalog.GetMasterPrefab(cloneMasterIndex);
                if (!cloneMasterPrefab)
                    return;
                
                Vector3 cloneSpawnPosition = targetBody.footPosition;
                Quaternion cloneSpawnRotation = ((Component)targetBody).transform.rotation;
                if (targetBody.characterDirection)
                {
                    cloneSpawnRotation = Quaternion.Euler(0f, targetBody.characterDirection.yaw, 0f);
                }

                Vector3 teleportOffset = UnityEngine.Random.insideUnitSphere * 2f;
                teleportOffset.y = (Mathf.Abs(teleportOffset.y) * 0.2f) + (equipmentSlot.characterBody.bestFitRadius * 1.5f);

                Vector3 teleportTargetPosition = equipmentSlot.characterBody.corePosition + teleportOffset;

                Vector3 teleportDirection = (teleportTargetPosition - targetBody.corePosition).normalized;

                Vector3 teleportDirectionHorizontal = teleportDirection;
                teleportDirectionHorizontal.y = 0f;
                teleportDirectionHorizontal.Normalize();

                TeleportHelper.TeleportBodyArgs targetBodyTeleportArgs = new TeleportHelper.TeleportBodyArgs
                {
                    body = targetBody,
                    forceOutOfVehicle = true,
                    resetStateMachines = false,
                    targetPosition = teleportTargetPosition,
                };

                if (targetBody.hasEffectiveAuthority)
                {
                    TeleportHelper.TeleportBody(targetBodyTeleportArgs);
                }
                else
                {
                    targetBody.CallRpcTeleportWithLocalAuthority(targetBodyTeleportArgs);
                }

                IPhysMotor targetMotor = targetBody.characterMotor ? targetBody.characterMotor : targetBody.GetComponent<IPhysMotor>();
                if (targetMotor is null or PseudoCharacterMotor ||
                    (targetMotor is not CharacterMotor && (!targetBody.TryGetComponent(out Rigidbody targetRigidbody) || targetRigidbody.isKinematic)))
                {
                    GameObject thrownObjectProjectile = ProjectileManager.instance.FireProjectileImmediateServer(new FireProjectileInfo
                    {
                        projectilePrefab = _thrownObjectProjectileSilentPrefab,
                        owner = equipmentSlot.gameObject,
                        position = teleportTargetPosition,
                        rotation = Quaternion.LookRotation(teleportDirectionHorizontal, Vector3.up),
                        speedOverride = 5f,
                        passenger = targetBody.gameObject,
                    });

                    if (thrownObjectProjectile.TryGetComponent(out ThrownObjectProjectileController thrownObjectProjectileController))
                    {
                        thrownObjectProjectileController.SetPassengerServer(targetBody.gameObject);
                    }
                }
                else
                {
                    targetMotor?.ApplyForceImpulse(new PhysForceInfo
                    {
                        force = teleportDirectionHorizontal * 5f,
                        ignoreGroundStick = true,
                        massIsOne = true,
                        resetVelocity = true
                    });
                }

                EffectManager.SpawnEffect(_teleportEffectPrefab, new EffectData
                {
                    origin = teleportTargetPosition,
                    rotation = Util.QuaternionSafeLookRotation(-teleportDirection, Vector3.up),
                }, true);

                BaseAI targetBodyAIController = targetBody.master ? targetBody.master.GetComponent<BaseAI>() : null;

                MasterSummon masterSummon = new MasterSummon
                {
                    summonerBodyObject = equipmentSlot.characterBody.gameObject,
                    masterPrefab = cloneMasterPrefab,
                    ignoreTeamMemberLimit = true,
                    position = cloneSpawnPosition,
                    rotation = cloneSpawnRotation,
                    inventoryToCopy = targetBody.inventory
                };

                if (targetBody.master)
                {
                    masterSummon.loadout = targetBody.master.loadout;
                }

                masterSummon.preSpawnSetupCallback += preSpawnSetup;

                CharacterMaster summonedMaster = masterSummon.Perform();
                if (summonedMaster)
                {
                    GameObject summonedBodyObject = summonedMaster.GetBodyObject();
                    if (summonedBodyObject)
                    {
                        foreach (EntityStateMachine stateMachine in summonedBodyObject.GetComponents<EntityStateMachine>())
                        {
                            stateMachine.initialStateType = stateMachine.mainStateType;
                        }
                    }
                }

                void preSpawnSetup(CharacterMaster spawnedMaster)
                {
                    if (!spawnedMaster)
                        return;

                    spawnedMaster.inventory.GiveItemPermanent(RoR2Content.Items.Ghost);
                    spawnedMaster.inventory.GiveItemPermanent(RoR2Content.Items.BoostDamage, 10);

                    int cloneDuration;
                    switch (qualityTier)
                    {
                        case QualityTier.Uncommon:
                            cloneDuration = 15;
                            break;
                        case QualityTier.Rare:
                            cloneDuration = 20;
                            break;
                        case QualityTier.Epic:
                            cloneDuration = 40;
                            break;
                        case QualityTier.Legendary:
                            cloneDuration = 60;
                            break;
                        default:
                            cloneDuration = 10;
                            Log.Warning($"Quality tier {qualityTier} is not implemented");
                            break;
                    }

                    spawnedMaster.inventory.GiveItemPermanent(ItemQualitiesContent.Items.TrueKillOnTimer, cloneDuration);

                    Deployable deployable = spawnedMaster.EnsureComponent<Deployable>();
                    deployable.onUndeploy ??= new UnityEvent();
                    deployable.onUndeploy.AddListener(spawnedMaster.TrueKill);

                    if (equipmentSlot.characterBody && equipmentSlot.characterBody.master)
                    {
                        equipmentSlot.characterBody.master.AddDeployable(deployable, _woodspriteCloneDeployableSlot);
                    }

                    if (targetBodyAIController && spawnedMaster.TryGetComponent(out BaseAI spawnedMasterAIController))
                    {
                        spawnedMasterAIController.currentEnemy = targetBodyAIController.currentEnemy;
                        spawnedMasterAIController.leader = targetBodyAIController.leader;
                        spawnedMasterAIController.buddy = targetBodyAIController.buddy;
                        spawnedMasterAIController.customTarget = targetBodyAIController.customTarget;

                        spawnedMasterAIController.UpdateTargets();
                    }
                }
            }
        }
    }
}
