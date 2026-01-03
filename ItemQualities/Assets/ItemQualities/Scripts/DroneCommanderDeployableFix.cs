using ItemQualities.Utilities.Extensions;
using RoR2;
using UnityEngine;
using UnityEngine.Events;

namespace ItemQualities
{
    internal sealed class DroneCommanderDeployableFix : MonoBehaviour
    {
        [SystemInitializer]
        static void Init()
        {
            if (DLC1Content.BodyPrefabs.DroneCommanderBody)
            {
                if (DLC1Content.BodyPrefabs.DroneCommanderBody.TryGetComponent(out Deployable deployable))
                {
                    DroneCommanderDeployableFix deployableFix = DLC1Content.BodyPrefabs.DroneCommanderBody.gameObject.AddComponent<DroneCommanderDeployableFix>();

                    deployable.onUndeploy ??= new UnityEvent();
                    deployable.onUndeploy.AddPersistentListener(deployableFix.OnUndeploy);
                }
                else
                {
                    Log.Error($"{DLC1Content.BodyPrefabs.DroneCommanderBody.name} is missing Deployable component");
                }
            }
        }

        CharacterBody _body;

        void Awake()
        {
            _body = GetComponent<CharacterBody>();
        }

        public void OnUndeploy()
        {
            if (_body && _body.master)
            {
                _body.master.TrueKill();
            }
        }
    }
}
