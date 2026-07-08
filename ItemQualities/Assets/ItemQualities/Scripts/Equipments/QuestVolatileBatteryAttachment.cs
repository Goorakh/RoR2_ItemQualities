using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace ItemQualities.Equipments
{
    public sealed class QuestVolatileBatteryAttachment : NetworkBehaviour
    {
        [SyncVar(hook = nameof(SyncVictimObject))]
        public GameObject victimObject;

        public bool crit;

        public CharacterBody victimBody { get; private set; }

        public override void OnStartClient()
        {
            base.OnStartClient();
            SyncVictimObject(victimObject);
        }

        private void SyncVictimObject(GameObject newVictimObject)
        {
            victimObject = newVictimObject;
            victimBody = newVictimObject ? newVictimObject.GetComponent<CharacterBody>() : null;
        }
    }
}
