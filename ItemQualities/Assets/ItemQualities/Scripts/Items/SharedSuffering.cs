using EntityStates;
using EntityStates.SharedSufferingOrb;
using HG.Coroutines;
using ItemQualities.ContentManagement;
using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using R2API;
using RoR2;
using RoR2.Projectile;
using RoR2BepInExPack.GameAssetPaths.Version_1_35_0;
using System.Collections;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Networking;
using UnityEngine.Rendering;
using UnityEngine.ResourceManagement.AsyncOperations;

using Random = UnityEngine.Random;

namespace ItemQualities.Items
{
    static class SharedSuffering
    {
        static DeployableSlot _sharedSufferingOrbSlot = DeployableSlot.None;

        static GameObject _sharedSufferingOrbProjectilePrefab;

        [InitDuringStartupPhase(GameInitPhase.PreFrame)]
        static void EarlyInit()
        {
            _sharedSufferingOrbSlot = DeployableAPI.RegisterDeployableSlot(getSharedSufferingOrbLimit);
        }

        static int getSharedSufferingOrbLimit(CharacterMaster self, int swarmsMultiplier)
        {
            return 3;
        }

        [ContentInitializer]
        static IEnumerator LoadContent(ContentIntializerArgs args)
        {
            AsyncOperationHandle<GameObject> timeCrystalBodyLoad = AddressableUtil.LoadTempAssetAsync<GameObject>(RoR2_Base_WeeklyRun.TimeCrystalBody_prefab);
            AsyncOperationHandle<GameObject> minorConstructOnKillProjectileLoad = AddressableUtil.LoadTempAssetAsync<GameObject>(RoR2_DLC1_MinorConstructOnKill.MinorConstructOnKillProjectile_prefab);
            AsyncOperationHandle<GameObject> minorConstructOnKillProjectileGhostLoad = AddressableUtil.LoadTempAssetAsync<GameObject>(RoR2_DLC1_MinorConstructOnKill.MinorConstructOnKillProjectileGhost_prefab);
            AsyncOperationHandle<Material> matTimeCrystalSolidLoad = AddressableUtil.LoadTempAssetAsync<Material>(RoR2_Base_crystalworld.matTimeCrystalSolid_mat);

            ParallelProgressCoroutine loadCoroutine = new ParallelProgressCoroutine(args.ProgressReceiver);
            loadCoroutine.Add(timeCrystalBodyLoad);
            loadCoroutine.Add(minorConstructOnKillProjectileLoad);
            loadCoroutine.Add(minorConstructOnKillProjectileGhostLoad);
            loadCoroutine.Add(matTimeCrystalSolidLoad);

            yield return loadCoroutine;

            if (!timeCrystalBodyLoad.AssertLoaded("TimeCrystalBody") ||
                !minorConstructOnKillProjectileLoad.AssertLoaded("MinorConstructOnKillProjectile") ||
                !minorConstructOnKillProjectileGhostLoad.AssertLoaded("MinorConstructOnKillProjectileGhost") ||
                !matTimeCrystalSolidLoad.AssertLoaded("matTimeCrystalSolid"))
            {
                yield break;
            }

            GameObject sharedSufferingOrbBodyPrefab = timeCrystalBodyLoad.Result.InstantiateClone("QualitySharedSufferingOrbBody");

            CharacterBody sharedSufferingOrbBody = sharedSufferingOrbBodyPrefab.GetComponent<CharacterBody>();
            sharedSufferingOrbBody.baseNameToken = "QUALITY_SHARED_SUFFERING_ORB_BODY_NAME";
            sharedSufferingOrbBody.baseMaxHealth = 1f;
            sharedSufferingOrbBody.levelMaxHealth = 0f;

            TeamComponent teamComponent = sharedSufferingOrbBodyPrefab.GetComponent<TeamComponent>();
            teamComponent.teamIndex = TeamIndex.Monster;

            NonSkillDamageModifier nonSkillDamageModifier = sharedSufferingOrbBodyPrefab.AddComponent<NonSkillDamageModifier>();
            nonSkillDamageModifier._damageCoeficient = 0f;

            EntityStateMachine entityStateMachine = sharedSufferingOrbBodyPrefab.GetComponent<EntityStateMachine>();
            entityStateMachine.initialStateType = new SerializableEntityStateType(typeof(SharedSufferingOrbSpawn));

            CharacterDeathBehavior deathBehavior = sharedSufferingOrbBodyPrefab.GetComponent<CharacterDeathBehavior>();
            deathBehavior.deathState = new SerializableEntityStateType(typeof(SharedSufferingOrbDeath));

            sharedSufferingOrbBodyPrefab.AddComponent<Deployable>();
            sharedSufferingOrbBodyPrefab.AddComponent<GenericOwnership>();

            SharedSufferingOrbController orbController = sharedSufferingOrbBodyPrefab.AddComponent<SharedSufferingOrbController>();

            ModelLocator modelLocator = sharedSufferingOrbBodyPrefab.GetComponent<ModelLocator>();
            Transform modelTransform = modelLocator ? modelLocator.modelTransform : null;
            if (modelTransform &&
                modelTransform.TryGetComponent(out ModelSkinController modelSkinController))
            {
                SkinDef skinDef = ScriptableObject.CreateInstance<SkinDef>();
                skinDef.name = "skinSharedSufferingOrbDefault";
                skinDef.skinDefParams = ScriptableObject.CreateInstance<SkinDefParams>();
                skinDef.skinDefParams.name = skinDef.name + "_params";

                skinDef.rootObject = modelLocator.modelTransform.gameObject;

                if (modelLocator.modelTransform.TryGetComponent(out MeshRenderer renderer))
                {
                    Material material = Material.Instantiate(matTimeCrystalSolidLoad.Result);
                    material.name = "matSharedSufferingOrb";
                    material.SetColor("_Color", new Color32(75, 221, 164, 255));
                    material.SetColor("_EmColor", new Color32(39, 93, 70, 255));

                    skinDef.skinDefParams.rendererInfos = new CharacterModel.RendererInfo[]
                    {
                        new CharacterModel.RendererInfo
                        {
                            renderer = renderer,
                            defaultMaterial = material,
                            defaultShadowCastingMode = ShadowCastingMode.On
                        }
                    };

                    skinDef.skinDefParams.meshReplacements = new SkinDefParams.MeshReplacement[]
                    {
                        new SkinDefParams.MeshReplacement
                        {
                            renderer = renderer,
                            meshAddress = new AssetReferenceT<Mesh>(RoR2_Base_crystalworld.crystalworld_props_fbx + "[CrystalMeshLarge]")
                        }
                    };
                }
                else
                {
                    Log.Error("Failed to find renderer for skin");
                }

                Light light = modelLocator.modelTransform.GetComponentInChildren<Light>();
                if (light)
                {
                    skinDef.skinDefParams.lightReplacements = new CharacterModel.LightInfo[]
                    {
                        new CharacterModel.LightInfo
                        {
                            light = light,
                            defaultColor = new Color32(0x1B, 0xF9, 0x6D, 0xFF)
                        }
                    };
                }
                else
                {
                    Log.Error("Failed to find light for skin");
                }

                modelSkinController.skins = new SkinDef[] { skinDef };
            }
            else
            {
                Log.Error("Failed to find skin controller");
            }

            Transform beam = modelTransform ? modelTransform.Find("Beam") : null;
            if (beam)
            {
                beam.gameObject.SetActive(false);
            }
            else
            {
                Log.Warning("Failed to find beam");
            }

            Transform radiusIndicator = sharedSufferingOrbBodyPrefab.transform.Find("ModelBase/WarningRadius");
            if (radiusIndicator)
            {
                if (radiusIndicator.TryGetComponent(out Renderer radiusIndicatorRenderer))
                {
                    Material indicatorMaterial = args.ContentPack.materials.Find("matSharedSufferingOrbAreaIndicator");
                    if (indicatorMaterial)
                    {
                        radiusIndicatorRenderer.sharedMaterial = indicatorMaterial;
                    }
                }

                orbController.RadiusIndicator = radiusIndicator;
            }
            else
            {
                Log.Error("Failed to find radius indicator");
            }

            _sharedSufferingOrbProjectilePrefab = minorConstructOnKillProjectileLoad.Result.InstantiateClone("QualitySharedSufferingOrbOnKillProjectile");
            GameObject.Destroy(_sharedSufferingOrbProjectilePrefab.GetComponent<ProjectileSpawnMaster>());

            ProjectileInstantiateDeployableOnImpact projectileInstantiateDeployableOnImpact = _sharedSufferingOrbProjectilePrefab.AddComponent<ProjectileInstantiateDeployableOnImpact>();
            projectileInstantiateDeployableOnImpact.DeployableSlot = _sharedSufferingOrbSlot;
            projectileInstantiateDeployableOnImpact.DeployablePrefab = sharedSufferingOrbBodyPrefab;

            args.ContentPack.bodyPrefabs.Add(sharedSufferingOrbBodyPrefab);

            args.ContentPack.projectilePrefabs.Add(_sharedSufferingOrbProjectilePrefab);
        }

        [SystemInitializer]
        static void Init()
        {
            GlobalEventManager.onCharacterDeathGlobal += onCharacterDeathGlobal;
        }

        static void onCharacterDeathGlobal(DamageReport damageReport)
        {
            if (!NetworkServer.active)
                return;

            if (!damageReport.attackerBody || !damageReport.attackerBody.inventory)
                return;
            
            ItemQualityCounts sharedSuffering = ItemQualitiesContent.ItemQualityGroups.SharedSuffering.GetItemCountsEffective(damageReport.attackerBody.inventory);
            if (sharedSuffering.TotalQualityCount > 0)
            {
                float spawnChance;
                switch (sharedSuffering.HighestQuality)
                {
                    case QualityTier.Uncommon:
                        spawnChance = 10f;
                        break;
                    case QualityTier.Rare:
                        spawnChance = 12f;
                        break;
                    case QualityTier.Epic:
                        spawnChance = 15f;
                        break;
                    case QualityTier.Legendary:
                        spawnChance = 20f;
                        break;
                    default:
                        Log.Error($"Quality tier {sharedSuffering.HighestQuality} is not implemented");
                        spawnChance = 0f;
                        break;
                }

                if (Util.CheckRoll(spawnChance, damageReport.attackerMaster))
                {
                    ProjectileManager.instance.FireProjectile(new FireProjectileInfo
                    {
                        projectilePrefab = _sharedSufferingOrbProjectilePrefab,
                        position = damageReport.victimBody ? damageReport.victimBody.corePosition : damageReport.damageInfo.position,
                        rotation = Quaternion.LookRotation(Quaternion.AngleAxis(Random.Range(-180f, 180f), Vector3.up) * Quaternion.AngleAxis(-80f, Vector3.right) * Vector3.forward),
                        owner = damageReport.attackerBody.gameObject
                    });
                }
            }
        }
    }
}
