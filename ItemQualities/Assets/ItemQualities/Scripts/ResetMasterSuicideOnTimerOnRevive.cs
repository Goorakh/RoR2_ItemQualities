using RoR2;
using UnityEngine;

namespace ItemQualities
{
    internal sealed class ResetMasterSuicideOnTimerOnRevive : MonoBehaviour
    {
        private CharacterMaster master;
        private MasterSuicideOnTimer masterSuicideOnTimer;

        private void Start()
        {
            master = GetComponent<CharacterMaster>();
            masterSuicideOnTimer = GetComponent<MasterSuicideOnTimer>();

            master.onBodyStart += onBodyStart;
        }

        private void OnDestroy()
        {
            master.onBodyStart -= onBodyStart;
        }

        private void onBodyStart(CharacterBody body)
        {
            if (masterSuicideOnTimer)
            {
                masterSuicideOnTimer.hasDied = false;
                masterSuicideOnTimer.timer = 0f;
            }
        }
    }
}
