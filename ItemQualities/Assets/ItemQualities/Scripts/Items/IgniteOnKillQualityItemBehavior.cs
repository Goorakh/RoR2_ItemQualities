using HG;
using HG.Coroutines;
using ItemQualities.ContentManagement;
using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using R2API;
using RoR2;
using RoR2BepInExPack.GameAssetPathsBetter;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace ItemQualities.Items
{
    public sealed class IgniteOnKillQualityItemBehavior : QualityItemBodyBehavior
    {
        static GameObject _fireAuraPrefab;

        static readonly SphereSearch _igniteOnKillSphereSearch = new SphereSearch();

        [ContentInitializer]
        static IEnumerator LoadContent(ContentIntializerArgs args)
        {
            AsyncOperationHandle<GameObject> icicleAuraLoad = AddressableUtil.LoadTempAssetAsync<GameObject>(RoR2_Base_Icicle.IcicleAura_prefab);

            ParallelProgressCoroutine prefabsLoadCoroutine = new ParallelProgressCoroutine(args.ProgressReceiver);
            prefabsLoadCoroutine.Add(icicleAuraLoad);
            
            yield return prefabsLoadCoroutine;

            if (icicleAuraLoad.Status != AsyncOperationStatus.Succeeded || !icicleAuraLoad.Result)
            {
                Log.Error($"Failed to load icicle Aura prefab: {icicleAuraLoad.OperationException}");
                yield break;
            }

            _fireAuraPrefab = icicleAuraLoad.Result.InstantiateClone("FireAura");

            IcicleAuraController icicleAura = _fireAuraPrefab.GetComponent<IcicleAuraController>();
            icicleAura.icicleMaxPerStack = 0;
            icicleAura.icicleBaseRadius = 1f;
            icicleAura.icicleRadiusPerIcicle = 2.5f;
            Destroy(icicleAura.buffWard);

            Transform particles = _fireAuraPrefab.transform.Find("Particles");
            if (particles)
            {
                setColor(particles, "Chunks");
                setColor(particles, "Ring, Core");
                setColor(particles, "Ring, Outer");
                setColor(particles, "Ring, Procced");
                setColor(particles, "SpinningSharpChunks");
                setColor(particles, "Area");
            }
            else
            {
                Log.Error($"Failed to find Particles in icicle Aura prefab");
            }

            args.ContentPack.networkedObjectPrefabs.Add(_fireAuraPrefab);
        }

        static void setColor(Transform particles, string childName)
        {
            Transform child = particles.Find(childName);
            if (child && child.TryGetComponent(out ParticleSystemRenderer particleSystemRenderer))
            {
                Material material = particleSystemRenderer.sharedMaterial;
                if (material)
                {
                    Material redMaterial = new Material(material);
                    redMaterial.name = $"{material.name}_Red";

                    redMaterial.SetColor(ShaderProperties._TintColor, new Color(1f, 0.1f, 0f));
                    redMaterial.SetColor(ShaderProperties._Color, new Color(1f, 0.1f, 0f));

                    particleSystemRenderer.sharedMaterial = redMaterial;
                }
            }
        }

        [ItemGroupAssociation(QualityItemBehaviorUsageFlags.Server)]
        static ItemQualityGroup GetItemGroup()
        {
            return ItemQualitiesContent.ItemQualityGroups.IgniteOnKill;
        }

        IcicleAuraController _icicleAura;
        GameObject _fireAuraObj;

        void OnEnable()
        {
            GlobalEventManager.onCharacterDeathGlobal += onCharacterDeathGlobal;

            _fireAuraObj = Instantiate(_fireAuraPrefab, transform.position, Quaternion.identity);

            _icicleAura = _fireAuraObj.GetComponent<IcicleAuraController>();
            _icicleAura.Networkowner = gameObject;

            NetworkServer.Spawn(_fireAuraObj);
        }

        void OnDisable()
        {
            GlobalEventManager.onCharacterDeathGlobal -= onCharacterDeathGlobal;

            if (_icicleAura)
            {
                Destroy(_icicleAura);
                _icicleAura = null;
            }
        }

        void onCharacterDeathGlobal(DamageReport damageReport)
        {
            if (damageReport == null || damageReport.attackerBody != Body || !_icicleAura)
                return;

            DotController victimDotController = DotController.FindDotController(damageReport.victimBody.gameObject);

            // Gas also ignites the enemy you killed, so needs to check for greater 1 instead
            if (damageReport.victimBody.GetBuffCount(RoR2Content.Buffs.OnFire) > 1 ||
                damageReport.victimBody.GetBuffCount(DLC1Content.Buffs.StrongerBurn) > 1 ||
                (victimDotController && victimDotController.HasDotActive(DotController.DotIndex.Helfire)))
            {
                _icicleAura.OnOwnerKillOther();
            }
        }

        protected override void OnStacksChanged()
        {
            base.OnStacksChanged();

            ItemQualityCounts igniteOnKill = ItemQualitiesContent.ItemQualityGroups.IgniteOnKill.GetItemCountsEffective(Body.inventory);

            _icicleAura.icicleDamageCoefficientPerTick = (1 * igniteOnKill.UncommonCount) +
                                                         (2 * igniteOnKill.RareCount) +
                                                         (3 * igniteOnKill.EpicCount) +
                                                         (5 * igniteOnKill.LegendaryCount);

            switch (igniteOnKill.HighestQuality)
            {
                case QualityTier.Uncommon:
                    _icicleAura.baseIcicleMax = 4;
                    _icicleAura.icicleDuration = 3;
                    break;
                case QualityTier.Rare:
                    _icicleAura.baseIcicleMax = 8;
                    _icicleAura.icicleDuration = 5;
                    break;
                case QualityTier.Epic:
                    _icicleAura.baseIcicleMax = 12;
                    _icicleAura.icicleDuration = 7;
                    break;
                case QualityTier.Legendary:
                    _icicleAura.baseIcicleMax = 20;
                    _icicleAura.icicleDuration = 10;
                    break;
            }
        }

        void FixedUpdate()
        {
            if (!NetworkServer.active)
                return;

            if (!_icicleAura)
                return;

            if (_icicleAura.finalIcicleCount <= 0)
                return;

            if (_icicleAura.attackStopwatch >= _icicleAura.baseIcicleAttackInterval ||
                _icicleAura.attackStopwatch == 0)
            {
                using var _ = ListPool<HurtBox>.RentCollection(out List<HurtBox> hurtBoxes);

                _igniteOnKillSphereSearch.origin = Body.corePosition;
                _igniteOnKillSphereSearch.mask = LayerIndex.entityPrecise.mask;
                _igniteOnKillSphereSearch.radius = _icicleAura.actualRadius;
                _igniteOnKillSphereSearch.RefreshCandidates();
                _igniteOnKillSphereSearch.FilterCandidatesByHurtBoxTeam(TeamMask.GetUnprotectedTeams(Body.teamComponent.teamIndex));
                _igniteOnKillSphereSearch.FilterCandidatesByDistinctHurtBoxEntities();
                _igniteOnKillSphereSearch.GetHurtBoxes(hurtBoxes);
                _igniteOnKillSphereSearch.ClearCandidates();

                foreach (HurtBox hurtBox in hurtBoxes)
                {
                    if (hurtBox.healthComponent && hurtBox.healthComponent.body != Body)
                    {
                        InflictDotInfo dotInfo = new InflictDotInfo
                        {
                            victimObject = hurtBox.healthComponent.gameObject,
                            attackerObject = Body.gameObject,
                            totalDamage = Body.damage * 1f,
                            dotIndex = DotController.DotIndex.Burn,
                            damageMultiplier = 1f
                        };

                        if (Body.inventory)
                        {
                            StrengthenBurnUtils.CheckDotForUpgrade(Body.inventory, ref dotInfo);
                        }

                        DotController.InflictDot(ref dotInfo);
                    }
                }
            }
        }
    }
}
