using HG;
using ItemQualities.Utilities.Extensions;
using RoR2;
using UnityEngine;
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

        CharacterBodyExtraStatsTracker _bodyExtraStatsComponent;

        [SyncVar(hook = nameof(hookSetSteakBonus))]
        public float SteakBonus;

        [SyncVar(hook = nameof(hookSetSpeedOnPickupBonus))]
        public int SpeedOnPickupBonus;

        int _stageIncomingDamageInstanceCountServer;
        public int StageDamageInstancesTakenCount => _stageIncomingDamageInstanceCountServer;

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
            _master.onBodyStart += onBodyStart;
            refreshBodyStatsComponentReference(_master.GetBodyObject());

            Stage.onServerStageBegin += onServerStageBegin;
        }

        void OnDisable()
        {
            _master.onBodyStart -= onBodyStart;
            Stage.onServerStageBegin -= onServerStageBegin;
        }

        void onBodyStart(CharacterBody body)
        {
            refreshBodyStatsComponentReference(body ? body.gameObject : null);
        }

        void refreshBodyStatsComponentReference(GameObject bodyObject)
        {
            _bodyExtraStatsComponent = bodyObject ? bodyObject.GetComponentCached<CharacterBodyExtraStatsTracker>() : null;
        }

        void onServerStageBegin(Stage stage)
        {
            _stageIncomingDamageInstanceCountServer = 0;
        }

        void markBodyStatsDirty()
        {
            CharacterBody body = _master ? _master.GetBody() : null;
            if (body)
            {
                body.MarkAllStatsDirty();
            }

            if (_bodyExtraStatsComponent)
            {
                _bodyExtraStatsComponent.MarkAllStatsDirty();
            }
        }

        public void OnIncomingDamageServer(DamageInfo damageInfo)
        {
            if (damageInfo.damage > 0f && !damageInfo.delayedDamageSecondHalf)
            {
                _stageIncomingDamageInstanceCountServer++;
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
    }
}
