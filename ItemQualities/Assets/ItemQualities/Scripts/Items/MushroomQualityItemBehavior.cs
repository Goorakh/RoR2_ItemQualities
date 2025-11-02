using EntityStates;
using EntityStates.MushroomBubbleDeployController;
using HG.Coroutines;
using ItemQualities.ContentManagement;
using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using R2API;
using RoR2;
using RoR2.Projectile;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace ItemQualities.Items
{
    public class MushroomQualityItemBehavior : MonoBehaviour
    {
		static GameObject _bubbleShieldPrefab;
		static EntityStateConfiguration _entityState;

		CharacterBody _body;
		GameObject _currentShield;
		EntityStateMachine _entityStateMachine;

		[ContentInitializer]
		static IEnumerator LoadContent(ContentIntializerArgs args)
		{
			AsyncOperationHandle<GameObject> bubbleShieldLoad = AddressableUtil.LoadTempAssetAsync<GameObject>(RoR2BepInExPack.GameAssetPathsBetter.RoR2_Base_Engi.EngiBubbleShield_prefab);

			ParallelProgressCoroutine prefabsLoadCoroutine = new ParallelProgressCoroutine(args.ProgressReceiver);
			prefabsLoadCoroutine.Add(bubbleShieldLoad);

			yield return prefabsLoadCoroutine;

			if (bubbleShieldLoad.Status != AsyncOperationStatus.Succeeded || !bubbleShieldLoad.Result)
			{
				Log.Error($"Failed to load Engie Bubble Shield prefab: {bubbleShieldLoad.OperationException}");
				yield break;
			}

			_bubbleShieldPrefab = bubbleShieldLoad.Result.InstantiateClone("MushroomShield");
			Destroy(_bubbleShieldPrefab.GetComponent<ApplyTorqueOnStart>());
			Destroy(_bubbleShieldPrefab.GetComponent<ProjectileStickOnImpact>());
			Destroy(_bubbleShieldPrefab.GetComponent<ProjectileController>());
			Destroy(_bubbleShieldPrefab.GetComponent<ProjectileDamage>());
			Destroy(_bubbleShieldPrefab.GetComponent<ProjectileNetworkTransform>());
			Destroy(_bubbleShieldPrefab.GetComponent<ProjectileSimple>());
			Destroy(_bubbleShieldPrefab.GetComponent<Rigidbody>());
			_bubbleShieldPrefab.GetComponent<EntityStateMachine>().initialStateType = new SerializableEntityStateType(typeof(MushroomBubbleDeploy));
			ChildLocator childLocator = _bubbleShieldPrefab.GetComponent<ChildLocator>();
			if (childLocator)
			{
				childLocator.FindChild("Bubble").gameObject.SetActive(value: true);
			}
			_bubbleShieldPrefab.AddComponent<GetOwnerBody>();

			args.ContentPack.networkedObjectPrefabs.Add(_bubbleShieldPrefab);
		}

		void Awake()
		{
			_body = GetComponent<CharacterBody>();
		}

		void FixedUpdate()
		{
			if (!NetworkServer.active) return;
			if (_body.notMovingStopwatch > 0.25f) {
				if (!_currentShield)
				{
					_currentShield = Object.Instantiate(_bubbleShieldPrefab, base.transform.position, Quaternion.identity);
					_currentShield.GetComponent<GetOwnerBody>().body = _body;
					NetworkServer.Spawn(_currentShield);
				}
			} else {
				_currentShield = null;
			}
		}
	}

	public class GetOwnerBody : MonoBehaviour {
		public CharacterBody body;
	}
}
