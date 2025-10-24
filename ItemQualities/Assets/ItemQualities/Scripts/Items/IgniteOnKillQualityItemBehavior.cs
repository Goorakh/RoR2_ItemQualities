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
	public class IgniteOnKillQualityItemBehavior : MonoBehaviour
    {
		static GameObject icicleAuraPrefab;
		IcicleAuraController icicleAura;
		CharacterBody body;

		private static readonly SphereSearch igniteOnKillSphereSearch = new SphereSearch();
		private static readonly List<HurtBox> fireAuraHurtBoxBuffer = new List<HurtBox>();

		void Awake()
		{
			body = GetComponent<CharacterBody>();
		}

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

			icicleAuraPrefab = icicleAuraLoad.Result.InstantiateClone("FireAura");

			IcicleAuraController icicleAura = icicleAuraPrefab.GetComponent<IcicleAuraController>();
			icicleAura.icicleMaxPerStack = 0;
			icicleAura.icicleBaseRadius = 1f;
			icicleAura.icicleRadiusPerIcicle = 2.5f;
			icicleAura.buffWard.buffDef = null;

			Transform particles = icicleAuraPrefab.transform.Find("Particles");
			if(particles == null) {
				Log.Error($"Failed to find Particles in icicle Aura prefab");
				yield break;
			}
			setColor(particles, "Chunks");
			setColor(particles, "Ring, Core");
			setColor(particles, "Ring, Outer");
			setColor(particles, "Ring, Procced");
			setColor(particles, "SpinningSharpChunks");
			setColor(particles, "Area");
		}

		static void setColor(Transform particles, string childName)
		{
			Transform child = particles.Find(childName);
			if (!child) return;
			ParticleSystemRenderer particleSystem = child.GetComponent<ParticleSystemRenderer>();
			if (!particles) return;

			Color color = new Color(1f, 0.1f, 0f);
			particleSystem.material.SetColor("_TintColor", color);
			particleSystem.material.SetColor("_Color", color);
		}

		void OnEnable()
		{
			GlobalEventManager.onCharacterDeathGlobal += OnCharacterDeathGlobal;
			GameObject gameObject = Object.Instantiate(icicleAuraPrefab, base.transform.position, Quaternion.identity);
			gameObject.transform.parent = base.transform;
			icicleAura = gameObject.GetComponent<IcicleAuraController>();
			icicleAura.Networkowner = base.gameObject;
			NetworkServer.Spawn(gameObject);
		}

		void OnDisable()
		{
			GlobalEventManager.onCharacterDeathGlobal -= OnCharacterDeathGlobal;
			if (icicleAura)
			{
				Object.Destroy(icicleAura);
				icicleAura = null;
			}
		}

		void OnCharacterDeathGlobal(DamageReport damageReport)
		{
			if (damageReport != null && damageReport.attackerBody == body && icicleAura && 
				damageReport.victimBody.GetBuffCount(RoR2Content.Buffs.OnFire) > 1)
			{
				icicleAura.OnOwnerKillOther();
			}
		}

		void FixedUpdate()
		{
			if (!NetworkServer.active) return;
			if (!icicleAura) return;
			if (!body.master) return;
			if (icicleAura.finalIcicleCount <= 0) return;
			if (icicleAura.attackStopwatch >= icicleAura.baseIcicleAttackInterval ||
				icicleAura.attackStopwatch == 0)
			{
				igniteOnKillSphereSearch.origin = body.corePosition;
				igniteOnKillSphereSearch.mask = LayerIndex.entityPrecise.mask;
				igniteOnKillSphereSearch.radius = icicleAura.actualRadius;
				igniteOnKillSphereSearch.RefreshCandidates();
				igniteOnKillSphereSearch.FilterCandidatesByHurtBoxTeam(TeamMask.GetUnprotectedTeams(body.master.teamIndex));
				igniteOnKillSphereSearch.FilterCandidatesByDistinctHurtBoxEntities();
				igniteOnKillSphereSearch.OrderCandidatesByDistance();
				igniteOnKillSphereSearch.GetHurtBoxes(fireAuraHurtBoxBuffer);
				igniteOnKillSphereSearch.ClearCandidates();
				for (int i = 0; i < fireAuraHurtBoxBuffer.Count; i++)
				{
					HurtBox hurtBox = fireAuraHurtBoxBuffer[i];
					if ((bool)hurtBox.healthComponent)
					{
						InflictDotInfo dotInfo = new InflictDotInfo
						{
							victimObject = hurtBox.healthComponent.gameObject,
							attackerObject = gameObject,
							totalDamage = body.damage * 1f,
							dotIndex = DotController.DotIndex.Burn,
							damageMultiplier = 1f
						};

						if (body.master.inventory)
						{
							StrengthenBurnUtils.CheckDotForUpgrade(body.master.inventory, ref dotInfo);
						}
						DotController.InflictDot(ref dotInfo);
					}
				}
				fireAuraHurtBoxBuffer.Clear();
			}
		}
	}
}


