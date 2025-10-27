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

namespace ItemQualities
{
    public sealed class DelayedHealPackController : NetworkBehaviour
    {
        [ContentInitializer]
        static IEnumerator Init(ContentIntializerArgs args)
        {
            AsyncOperationHandle<GameObject> healPackLoad = AddressableUtil.LoadTempAssetAsync<GameObject>(RoR2_Base_Tooth.HealPack_prefab);
            healPackLoad.OnSuccess(healPackPrefab =>
            {
                GameObject delayedHealPackPrefab = healPackPrefab.InstantiateClone(nameof(ItemQualitiesContent.NetworkedPrefabs.HealPackDelayed));
                DelayedHealPackController delayedHealPackController = delayedHealPackPrefab.AddComponent<DelayedHealPackController>();
                delayedHealPackController._healthPickup = delayedHealPackPrefab.GetComponentInChildren<HealthPickup>();
                delayedHealPackController._gravitatePickup = delayedHealPackPrefab.GetComponentInChildren<GravitatePickup>();

                if (delayedHealPackController._healthPickup)
                {
                    delayedHealPackController._healthPickupTrigger = delayedHealPackController._healthPickup.GetComponent<Collider>();
                }

                if (delayedHealPackController._gravitatePickup)
                {
                    delayedHealPackController._gravitateTrigger = delayedHealPackController._gravitatePickup.GetComponent<Collider>();
                }

                delayedHealPackController.setBehaviorsEnabled(false);

                args.ContentPack.networkedObjectPrefabs.Add(delayedHealPackPrefab);
            });

            return healPackLoad.AsProgressCoroutine(args.ProgressReceiver);
        }

        [SyncVar]
        public float Delay = 1f;

        [SerializeField]
        HealthPickup _healthPickup;

        [SerializeField]
        Collider _healthPickupTrigger;

        [SerializeField]
        GravitatePickup _gravitatePickup;

        [SerializeField]
        Collider _gravitateTrigger;

        bool _reachedTimerEnd = false;
        float _timer = 0f;

        void OnEnable()
        {
            _timer = 0f;
            _reachedTimerEnd = false;

            setBehaviorsEnabled(false);
        }

        void FixedUpdate()
        {
            if (!_reachedTimerEnd)
            {
                _timer += Time.fixedDeltaTime;
                if (_timer >= Delay)
                {
                    _reachedTimerEnd = true;

                    setBehaviorsEnabled(true);
                }
            }
        }

        void setBehaviorsEnabled(bool enabled)
        {
            if (_healthPickup)
                _healthPickup.enabled = enabled;

            if (_healthPickupTrigger)
                _healthPickupTrigger.enabled = enabled;

            if (_gravitatePickup)
                _gravitatePickup.enabled = enabled;

            if (_gravitateTrigger)
                _gravitateTrigger.enabled = enabled;
        }
    }
}
