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

        void Awake()
        {
            _master = GetComponent<CharacterMaster>();
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
