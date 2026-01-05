using EntityStates;
using EntityStates.SharedSufferingOrb;
using HG;
using HG.Coroutines;
using ItemQualities.ContentManagement;
using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using R2API;
using RoR2;
using RoR2.Items;
using RoR2.Projectile;
using RoR2BepInExPack.GameAssetPaths.Version_1_39_0;
using System.Collections;
using System.Collections.Generic;
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
            sharedSufferingOrbBody.bodyFlags &= ~CharacterBody.BodyFlags.Ungrabbable;

            TeamComponent teamComponent = sharedSufferingOrbBodyPrefab.GetComponent<TeamComponent>();
            teamComponent.teamIndex = TeamIndex.Neutral;

            NonSkillDamageModifier nonSkillDamageModifier = sharedSufferingOrbBodyPrefab.EnsureComponent<NonSkillDamageModifier>();
            nonSkillDamageModifier._damageCoeficient = 0f;

            CharacterDeathBehavior deathBehavior = sharedSufferingOrbBodyPrefab.GetComponent<CharacterDeathBehavior>();
            deathBehavior.deathState = new SerializableEntityStateType(typeof(SharedSufferingOrbDeath));

            sharedSufferingOrbBodyPrefab.EnsureComponent<Deployable>();
            sharedSufferingOrbBodyPrefab.EnsureComponent<GenericOwnership>();

            SpecialObjectAttributes specialObjectAttributes = sharedSufferingOrbBodyPrefab.EnsureComponent<SpecialObjectAttributes>();
            specialObjectAttributes.renderersToDisable ??= new List<Renderer>();
            specialObjectAttributes.lightsToDisable ??= new List<Light>();

            SharedSufferingOrbController orbController = sharedSufferingOrbBodyPrefab.EnsureComponent<SharedSufferingOrbController>();

            ModelLocator modelLocator = sharedSufferingOrbBodyPrefab.GetComponent<ModelLocator>();
            Transform modelTransform = modelLocator ? modelLocator.modelTransform : null;
            if (modelTransform && modelTransform.TryGetComponent(out ModelSkinController modelSkinController))
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
                    material.SetColor(ShaderProperties._Color, new Color32(75, 221, 164, 255));
                    material.SetColor(ShaderProperties._EmissionColor, new Color32(39, 93, 70, 255));

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
                            meshAddress = new AssetReferenceT<Mesh>(RoR2_Base_crystalworld.crystalworld_props_fbx_CrystalMeshLarge_)
                        }
                    };

                    specialObjectAttributes.useSkillHighlightRenderers = true;
                    specialObjectAttributes.skillHighlightRenderers ??= new List<Renderer>();
                    specialObjectAttributes.skillHighlightRenderers.Add(renderer);
                }
                else
                {
                    Log.Error("Failed to find renderer for skin");
                }

                Light light = modelLocator.modelTransform.GetComponentInChildren<Light>();
                if (light)
                {
                    specialObjectAttributes.lightsToDisable.Add(light);

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
                if (radiusIndicator.TryGetComponent(out MeshFilter meshFilter))
                {
                    meshFilter.sharedMesh = MeshUtil.GetPrimitive(PrimitiveType.Sphere);
                }

                if (radiusIndicator.TryGetComponent(out Renderer radiusIndicatorRenderer))
                {
                    specialObjectAttributes.renderersToDisable.Add(radiusIndicatorRenderer);

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

            Transform particlesTransform = sharedSufferingOrbBodyPrefab.transform.Find("ModelBase/Swirls");
            if (particlesTransform && particlesTransform.TryGetComponent(out Renderer particlesRenderer))
            {
                specialObjectAttributes.renderersToDisable.Add(particlesRenderer);
            }
            else
            {
                Log.Warning("Failed to find particles");
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
            On.RoR2.Items.SharedSufferingItemBehaviour.TryAdd += SharedSufferingItemBehaviour_TryAdd;

            GlobalEventManager.onCharacterDeathGlobal += onCharacterDeathGlobal;
        }

        static bool SharedSufferingItemBehaviour_TryAdd(On.RoR2.Items.SharedSufferingItemBehaviour.orig_TryAdd orig, SharedSufferingItemBehaviour self, CharacterBody newTarget)
        {
            // Fix IndexOutOfRangeException in SharedSufferingManager if trying to add a body not on any team
            if (newTarget && newTarget.teamComponent && newTarget.teamComponent.teamIndex != TeamIndex.None)
            {
                return orig(self, newTarget);
            }

            return false;
        }

        static void onCharacterDeathGlobal(DamageReport damageReport)
        {
            if (!NetworkServer.active)
                return;

            if (!damageReport.attackerBody || !damageReport.attackerBody.inventory)
                return;
            
            ItemQualityCounts sharedSuffering = damageReport.attackerBody.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.SharedSuffering);
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

                if (RollUtil.CheckRoll(spawnChance, damageReport.attackerMaster, damageReport.damageInfo.procChainMask.HasProc(ProcType.SureProc)))
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
