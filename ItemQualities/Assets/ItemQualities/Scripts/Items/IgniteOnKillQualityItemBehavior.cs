using HG.Coroutines;
using ItemQualities.ContentManagement;
using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using R2API;
using RoR2;
using RoR2BepInExPack.GameAssetPathsBetter;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.ResourceManagement.AsyncOperations;


namespace ItemQualities.Items
{
	public class IgniteOnKillQualityItemBehavior : MonoBehaviour
    {
		static GameObject _fireAuraPrefab;
		private static readonly SphereSearch _igniteOnKillSphereSearch = new SphereSearch();
		private static readonly List<HurtBox> _fireAuraHurtBoxBuffer = new List<HurtBox>();

		IcicleAuraController _icicleAura;
		CharacterBody _body;
		GameObject _fireAuraObj;

		void Awake()
		{
			_body = GetComponent<CharacterBody>();
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

			_fireAuraPrefab = icicleAuraLoad.Result.InstantiateClone("FireAura");

			IcicleAuraController icicleAura = _fireAuraPrefab.GetComponent<IcicleAuraController>();
			icicleAura.icicleMaxPerStack = 0;
			icicleAura.icicleBaseRadius = 1f;
			icicleAura.icicleRadiusPerIcicle = 2.5f;
			Destroy(icicleAura.buffWard);

			Transform particles = _fireAuraPrefab.transform.Find("Particles");
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

			GasHandleStacks GasStacks = _fireAuraPrefab.AddComponent<GasHandleStacks>();
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
			_fireAuraObj = Object.Instantiate(_fireAuraPrefab, base.transform.position, Quaternion.identity);
			_icicleAura = _fireAuraObj.GetComponent<IcicleAuraController>();
			_icicleAura.Networkowner = base.gameObject;
			GasHandleStacks gasStacks = _fireAuraObj.GetComponent<GasHandleStacks>();
			gasStacks.owner = base.gameObject;
			_body.onInventoryChanged += gasStacks.OnInventoryChanged;
			gasStacks.OnInventoryChanged();

			NetworkServer.Spawn(_fireAuraObj);
		}

		void OnDisable()
		{
			GlobalEventManager.onCharacterDeathGlobal -= OnCharacterDeathGlobal;
			if (_icicleAura)
			{
				Object.Destroy(_icicleAura);
				_icicleAura = null;
			}
		}

		void OnCharacterDeathGlobal(DamageReport damageReport)
		{
			if (damageReport != null && damageReport.attackerBody == _body && _icicleAura && 
				damageReport.victimBody.GetBuffCount(RoR2Content.Buffs.OnFire) > 1) //gas also ignites the enemy you killed, so needs to check for greater 1 instead
			{
				_icicleAura.OnOwnerKillOther();
			}
		}

		void FixedUpdate()
		{
			if (!NetworkServer.active) return;
			if (!_icicleAura) return;
			if (!_body.master) return;
			if (_icicleAura.finalIcicleCount <= 0) return;
			if (_icicleAura.attackStopwatch >= _icicleAura.baseIcicleAttackInterval ||
				_icicleAura.attackStopwatch == 0)
			{
				_igniteOnKillSphereSearch.origin = _body.corePosition;
				_igniteOnKillSphereSearch.mask = LayerIndex.entityPrecise.mask;
				_igniteOnKillSphereSearch.radius = _icicleAura.actualRadius;
				_igniteOnKillSphereSearch.RefreshCandidates();
				_igniteOnKillSphereSearch.FilterCandidatesByHurtBoxTeam(TeamMask.GetUnprotectedTeams(_body.master.teamIndex));
				_igniteOnKillSphereSearch.FilterCandidatesByDistinctHurtBoxEntities();
				_igniteOnKillSphereSearch.GetHurtBoxes(_fireAuraHurtBoxBuffer);
				_igniteOnKillSphereSearch.ClearCandidates();
				for (int i = 0; i < _fireAuraHurtBoxBuffer.Count; i++)
				{
					HurtBox hurtBox = _fireAuraHurtBoxBuffer[i];
					if (hurtBox.healthComponent && hurtBox.healthComponent.body != _body)
					{
						InflictDotInfo dotInfo = new InflictDotInfo
						{
							victimObject = hurtBox.healthComponent.gameObject,
							attackerObject = gameObject,
							totalDamage = _body.damage * 1f,
							dotIndex = DotController.DotIndex.Burn,
							damageMultiplier = 1f
						};

						if (_body.master.inventory)
						{
							StrengthenBurnUtils.CheckDotForUpgrade(_body.master.inventory, ref dotInfo);
						}
						DotController.InflictDot(ref dotInfo);
					}
				}
				_fireAuraHurtBoxBuffer.Clear();
			}
		}
	}

	public class GasHandleStacks : NetworkBehaviour
	{
		private struct OwnerInfo
		{
			public readonly GameObject gameObject;
			public readonly CharacterBody body;
			public OwnerInfo(GameObject gameObject)
			{
				this.gameObject = gameObject;
				if ((bool)gameObject)
				{
					body = gameObject.GetComponent<CharacterBody>();
				}
				else
				{
					body = null;
				}
			}
		}

		private OwnerInfo _cachedOwnerInfo;
		private IcicleAuraController _icicleAura;

		[SyncVar]
		public GameObject owner;

		private void Awake()
		{
			_icicleAura = GetComponent<IcicleAuraController>();
		}

		public void OnInventoryChanged()
		{
			if (_cachedOwnerInfo.gameObject != owner)
			{
				_cachedOwnerInfo = new OwnerInfo(owner);
			}
			if (!_cachedOwnerInfo.body) return;
			if (!_icicleAura) return;
			ItemQualityCounts IgniteOnKill = ItemQualitiesContent.ItemQualityGroups.IgniteOnKill.GetItemCounts(_cachedOwnerInfo.body.master.inventory);
			_icicleAura.icicleDamageCoefficientPerTick = IgniteOnKill.UncommonCount * 1 +
														IgniteOnKill.RareCount * 2 +
														IgniteOnKill.EpicCount * 3 +
														IgniteOnKill.LegendaryCount * 5;

			switch (ItemQualitiesContent.ItemQualityGroups.IgniteOnKill.GetHighestQualityInInventory(_cachedOwnerInfo.body.master.inventory))
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
	}
}


