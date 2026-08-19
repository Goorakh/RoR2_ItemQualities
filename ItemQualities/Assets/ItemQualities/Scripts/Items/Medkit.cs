using ItemQualities.ContentManagement;
using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using R2API;
using RoR2;
using RoR2BepInExPack.GameAssetPathsBetter;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace ItemQualities.Items
{
    internal static class Medkit
    {
        private static GameObject _healingWardPrefab;

        [ContentInitializer]
        private static IEnumerator LoadContent(ContentInitializerArgs args)
        {
            AsyncOperationHandle<GameObject> shrineHealingWardHandle = AddressableUtil.LoadAssetAsync<GameObject>(RoR2_Base_ShrineHealing.ShrineHealingWard_prefab);
            shrineHealingWardHandle.OnSuccess(healingWardPrefab =>
            {
                _healingWardPrefab = healingWardPrefab.InstantiateClone("MedkitHealingWard");
                _healingWardPrefab.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);

                _healingWardPrefab.AddComponent<MedkitHealingWardController>();

                HealingWard healingWard = _healingWardPrefab.GetComponent<HealingWard>();
                healingWard.radius = 0f;
                healingWard.interval = 0.25f;
                healingWard.healPoints = 0f;
                healingWard.healFraction = 0f;

                args.ContentPack.networkedObjectPrefabs.Add(_healingWardPrefab);
            });

            return shrineHealingWardHandle.AsProgressCoroutine(args.ProgressReceiver);
        }

        [SystemInitializer]
        private static void Init()
        {
            GlobalEventManager.OnInteractionsGlobal += onInteractGlobal;
        }

        private static void onInteractGlobal(Interactor interactor, IInteractable interactable, GameObject interactableObject)
        {
            if (!NetworkServer.active)
                return;

            if (!SharedItemUtils.InteractableIsPermittedForSpawn(interactable))
                return;

            if (!interactor || !interactor.TryGetComponent(out CharacterBody interactorBody) || !interactorBody.inventory)
                return;

            ItemQualityCounts medkit = interactorBody.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.Medkit);
            if (medkit.TotalQualityCount > 0)
            {
                MedkitHealingWardController medkitHealingWard = MedkitHealingWardController.FindHealingWard(interactableObject, interactorBody.teamComponent.teamIndex);
                if (!medkitHealingWard)
                {
                    GameObject healingWardInstance = GameObject.Instantiate(_healingWardPrefab, interactableObject.transform.position, Quaternion.identity);

                    medkitHealingWard = healingWardInstance.GetComponent<MedkitHealingWardController>();

                    TeamFilter teamFilter = healingWardInstance.GetComponent<TeamFilter>();
                    teamFilter.teamIndex = interactorBody.teamComponent.teamIndex;

                    medkitHealingWard.InteractableObject = interactableObject;

                    NetworkServer.Spawn(healingWardInstance);
                }

                float radius = (7f * medkit.UncommonCount) +
                               (15f * medkit.RareCount) +
                               (25f * medkit.EpicCount) +
                               (40f * medkit.LegendaryCount);
                
                float healFractionPerSecond;
                float duration;
                switch (medkit.HighestQuality)
                {
                    case QualityTier.Uncommon:
                        healFractionPerSecond = 0.05f;
                        duration = 15f;
                        break;
                    case QualityTier.Rare:
                        healFractionPerSecond = 0.10f;
                        duration = 30f;
                        break;
                    case QualityTier.Epic:
                        healFractionPerSecond = 0.20f;
                        duration = 45f;
                        break;
                    case QualityTier.Legendary:
                        healFractionPerSecond = 0.30f;
                        duration = 60f;
                        break;
                    default:
                        healFractionPerSecond = 0f;
                        duration = 0f;
                        Log.Warning($"Quality tier {medkit.HighestQuality} is not implemented");
                        break;
                }

                medkitHealingWard.RadiusStackCount++;
                medkitHealingWard.HealingWard.Networkradius += radius / Mathf.Pow(medkitHealingWard.RadiusStackCount, 1.33f);
                medkitHealingWard.HealingWard.healFraction = healFractionPerSecond * medkitHealingWard.HealingWard.interval;
                medkitHealingWard.ResetDuration(duration);
            }
        }
    }

    public sealed class MedkitHealingWardController : NetworkBehaviour
    {
        [SyncVar(hook = nameof(syncInteractableObject))]
        public GameObject InteractableObject;

        public int RadiusStackCount;

        public HealingWard HealingWard { get; private set; }

        private TeamFilter _teamFilter;

        private float _age;
        private float _duration;

        private void Awake()
        {
            _teamFilter = GetComponent<TeamFilter>();
            HealingWard = GetComponent<HealingWard>();

            InstanceTracker.Add(this);
        }

        private void OnDestroy()
        {
            InstanceTracker.Remove(this);
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            syncInteractableObject(InteractableObject);
        }

        private void FixedUpdate()
        {
            if (NetworkServer.active)
            {
                _age += Time.fixedDeltaTime;
                if (_age >= _duration)
                {
                    Destroy(gameObject);
                }
            }
        }

        private void LateUpdate()
        {
            tryMatchInteractablePosition();
        }

        private void tryMatchInteractablePosition()
        {
            if (InteractableObject)
            {
                transform.position = InteractableObject.transform.position;
            }
        }

        public void OnAttachedObjectDiscovered(GenericNetworkedObjectAttachment attachment, GameObject attachedObject)
        {
            tryMatchInteractablePosition();
        }

        public void ResetDuration(float newDuration)
        {
            _duration = newDuration;
            _age = 0f;
        }

        private void syncInteractableObject(GameObject newInteractableObject)
        {
            InteractableObject = newInteractableObject;
            tryMatchInteractablePosition();
        }

        public static MedkitHealingWardController FindHealingWard(GameObject interactableObject, TeamIndex teamIndex)
        {
            if (!ReferenceEquals(interactableObject, null))
            {
                foreach (MedkitHealingWardController medkitHealingWard in InstanceTracker.GetInstancesList<MedkitHealingWardController>())
                {
                    if (medkitHealingWard._teamFilter.teamIndex == teamIndex &&
                        ReferenceEquals(medkitHealingWard.InteractableObject, interactableObject))
                    {
                        return medkitHealingWard;
                    }
                }
            }

            return null;
        }
    }
}
