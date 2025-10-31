using EntityStates;
using EntityStates.BossGroupHealNovaController;
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
    [RequireComponent(typeof(TeamFilter))]
    public class BossGroupHealNovaSpawner : NetworkBehaviour
    {
        static GameObject _pulsePrefab;

        [ContentInitializer]
        static IEnumerator LoadContent(ContentIntializerArgs args)
        {
            AsyncOperationHandle<GameObject> novaPulseLoad = AddressableUtil.LoadTempAssetAsync<GameObject>(RoR2_Base_TPHealingNova.TeleporterHealNovaPulse_prefab);
            novaPulseLoad.OnSuccess(novaPulse =>
            {
                _pulsePrefab = novaPulse.InstantiateClone("BossGroupHealNovaPulse");

                EntityStateMachine entityStateMachine = _pulsePrefab.GetComponent<EntityStateMachine>();
                entityStateMachine.initialStateType = new SerializableEntityStateType(typeof(BossGroupHealNovaWindup));
                entityStateMachine.mainStateType = new SerializableEntityStateType(typeof(BossGroupHealNovaPulse));

                args.ContentPack.networkedObjectPrefabs.Add(_pulsePrefab);
            });

            return novaPulseLoad.AsProgressCoroutine(args.ProgressReceiver);
        }

        public BossGroup BossGroup;

        public float MinSecondsBetweenPulses = 1f;

        [SyncVar]
        public float NovaRadius = 100f;

        TeamFilter _teamFilter;

        float _lastPulseFraction;

        float _pulseAvailableTimer = 0f;

        void Awake()
        {
            _teamFilter = GetComponent<TeamFilter>();
        }

        void Start()
        {
            if (NetworkServer.active)
            {
                _lastPulseFraction = getCurrentBossProgressFraction();
            }
        }

        void OnDisable()
        {
            transform.DetachChildren();
        }

        void FixedUpdate()
        {
            if (NetworkServer.active)
            {
                fixedUpdateServer(Time.fixedDeltaTime);
            }
        }

        void fixedUpdateServer(float deltaTime)
        {
            if (_pulseAvailableTimer > 0f)
            {
                _pulseAvailableTimer -= deltaTime;
            }
            else
            {
                ItemQualityCounts tpHealingNova = ItemQualitiesContent.ItemQualityGroups.TPHealingNova.GetTeamItemCounts(_teamFilter.teamIndex, true);

                int pulseCount = (1 * tpHealingNova.UncommonCount) +
                                 (2 * tpHealingNova.RareCount) +
                                 (3 * tpHealingNova.EpicCount) +
                                 (5 * tpHealingNova.LegendaryCount);

                float pulseFraction = getNextPulseFraction(pulseCount, _lastPulseFraction);
                float bossGroupProgressFraction = getCurrentBossProgressFraction();

                if (bossGroupProgressFraction > pulseFraction)
                {
                    spawnPulse();
                    _lastPulseFraction = pulseFraction;
                    _pulseAvailableTimer = MinSecondsBetweenPulses;
                }
            }
        }

        float getCurrentBossProgressFraction()
        {
            if (!BossGroup || !BossGroup.combatSquad)
                return 0f;
            
            if (BossGroup.combatSquad.defeatedServer)
                return 1f;

            float bossGroupMaxHealth = BossGroup.totalMaxObservedMaxHealth;
            float bossGroupCurrentHealth = BossGroup.totalObservedHealth;

            float bossHealthFraction = bossGroupMaxHealth > 0f ? Mathf.Clamp01(bossGroupCurrentHealth / bossGroupMaxHealth) : 1f;
            float bossGroupProgressFraction = 1f - bossHealthFraction;

            return bossGroupProgressFraction;
        }

        static float getNextPulseFraction(int pulseCount, float lastPulseFraction)
        {
            float pulseSegmentFraction = 1f / (pulseCount + 1);
            for (int i = 0; i < pulseCount; i++)
            {
                float pulseStartFraction = pulseSegmentFraction * (i + 1);
                if (pulseStartFraction > lastPulseFraction)
                {
                    return pulseStartFraction;
                }
            }

            return 1f;
        }

        void spawnPulse()
        {
            GameObject pulseObj = Instantiate(_pulsePrefab, transform.position, transform.rotation, transform);

            TeamFilter pulseTeamFilter = pulseObj.GetComponent<TeamFilter>();
            pulseTeamFilter.teamIndex = _teamFilter.teamIndex;

            NetworkServer.Spawn(pulseObj);
        }
    }
}
