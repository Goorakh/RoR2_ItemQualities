using HG;
using RoR2;
using UnityEngine.Networking;

namespace ItemQualities
{
    public class CharacterMasterExtraStatsTracker : NetworkBehaviour
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

        [SyncVar(hook = nameof(hookSetSteakBonus))]
        public float SteakBonus;

        int _stageIncomingDamageInstanceCountServer;
        public int StageDamageInstancesTakenCount => _stageIncomingDamageInstanceCountServer;

        void Awake()
        {
            _master = GetComponent<CharacterMaster>();
        }

        void OnEnable()
        {
            Stage.onServerStageBegin += onServerStageBegin;
        }

        void OnDisable()
        {
            Stage.onServerStageBegin -= onServerStageBegin;
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

                if (body.TryGetComponent(out CharacterBodyExtraStatsTracker bodyExtraStatsTracker))
                {
                    bodyExtraStatsTracker.MarkAllStatsDirty();
                }
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
    }
}
