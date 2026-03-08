using HG;
using ItemQualities.Utilities.Extensions;
using RoR2;
using System;
using UnityEngine.Networking;

namespace ItemQualities
{
    public sealed class CharacterMasterExtraStatsTracker : NetworkBehaviour
    {
        [SystemInitializer(typeof(MasterCatalog))]
        static void Init()
        {
            foreach (CharacterMaster master in MasterCatalog.allMasters)
            {
                if (master)
                {
                    master.gameObject.EnsureComponent<CharacterMasterExtraStatsTracker>();
                }
            }
        }

        CharacterMaster _master;

        CharacterBody _cachedBody;
        CharacterBodyExtraStatsTracker _bodyExtraStatsComponent;

        [SyncVar(hook = nameof(hookSetSteakBonus))]
        public float SteakBonus;

        [SyncVar(hook = nameof(hookSetSpeedOnPickupBonus))]
        public int SpeedOnPickupBonus;

        [SyncVar(hook = nameof(hookSetBossDamageBonusTicks))]
        public int BossDamageBonusTicks;

        [SyncVar]
        public int CardStoredInteractableIndex = -1;

        int _stageIncomingDamageInstanceCountServer;
        public int StageDamageInstancesTakenCount => _stageIncomingDamageInstanceCountServer;

        public event Action<CharacterMasterExtraStatsTracker> OnStageDamageInstancesTakenCountChangedServer;
        public event Action<CharacterMasterExtraStatsTracker> BossDamageBonusTicksChanged;

        void Awake()
        {
            _master = GetComponent<CharacterMaster>();

            ComponentCache.Add(gameObject, this);
        }

        void OnDestroy()
        {
            ComponentCache.Remove(gameObject, this);
        }

        void OnEnable()
        {
            _master.onBodyStart += setBody;
            _master.onBodyDestroyed += setBody;

            Stage.onServerStageBegin += onServerStageBegin;

            setBody(_master.GetBody());
        }

        void OnDisable()
        {
            _master.onBodyStart -= setBody;
            _master.onBodyDestroyed -= setBody;
            Stage.onServerStageBegin -= onServerStageBegin;

            setBody(null);
        }

        void setBody(CharacterBody body)
        {
            if (_cachedBody == body)
                return;

            if (_bodyExtraStatsComponent)
            {
                _bodyExtraStatsComponent.OnIncomingDamageServer -= onIncomingDamageServer;
            }

            _cachedBody = body;
            _bodyExtraStatsComponent = body ? body.GetComponentCached<CharacterBodyExtraStatsTracker>() : null;

            if (_bodyExtraStatsComponent)
            {
                _bodyExtraStatsComponent.OnIncomingDamageServer += onIncomingDamageServer;
            }
        }

        void onServerStageBegin(Stage stage)
        {
            if (_stageIncomingDamageInstanceCountServer != 0)
            {
                _stageIncomingDamageInstanceCountServer = 0;
                OnStageDamageInstancesTakenCountChangedServer?.Invoke(this);
            }
        }

        void onIncomingDamageServer(DamageInfo damageInfo)
        {
            if (damageInfo.damage > 0f && !damageInfo.delayedDamageSecondHalf && (damageInfo.damageType & DamageType.DoT) == 0)
            {
                _stageIncomingDamageInstanceCountServer++;
                OnStageDamageInstancesTakenCountChangedServer?.Invoke(this);
            }
        }

        void markBodyStatsDirty()
        {
            if (_cachedBody)
            {
                _cachedBody.MarkAllStatsDirty();
            }
        }

        void hookSetSteakBonus(float steakBonus)
        {
            bool changed = SteakBonus != steakBonus;
            SteakBonus = steakBonus;

            if (changed)
            {
                markBodyStatsDirty();
            }
        }

        void hookSetSpeedOnPickupBonus(int speedOnPickupBonus)
        {
            bool changed = SpeedOnPickupBonus != speedOnPickupBonus;
            SpeedOnPickupBonus = speedOnPickupBonus;

            if (changed)
            {
                markBodyStatsDirty();
            }
        }

        void hookSetBossDamageBonusTicks(int bossDamageBonusTicks)
        {
            bool changed = BossDamageBonusTicks != bossDamageBonusTicks;
            BossDamageBonusTicks = bossDamageBonusTicks;

            if (changed)
            {
                BossDamageBonusTicksChanged?.Invoke(this);
            }
        }
    }
}
