using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;

namespace ItemQualities
{
    public class EventFilter : MonoBehaviour
    {
        public NetworkConnectionType AllowedConnectionTypes = NetworkConnectionType.Any;

        public UnityEvent Event;

        public bool PassesConditions()
        {
            NetworkConnectionType networkConnectionType = NetworkConnectionType.None;
            if (NetworkServer.active)
            {
                networkConnectionType |= NetworkConnectionType.Server;

                if (NetworkClient.active)
                {
                    networkConnectionType |= NetworkConnectionType.Host;
                }
            }
            else if (NetworkClient.active)
            {
                networkConnectionType |= NetworkConnectionType.Client;
            }

            if ((AllowedConnectionTypes & networkConnectionType) == 0)
                return false;

            return true;
        }

        public void TryTrigger()
        {
            if (PassesConditions())
            {
                Event?.Invoke();
            }
        }

        [Flags]
        public enum NetworkConnectionType
        {
            None = 0,
            Client = 1 << 0,
            Host = 1 << 1,
            Server = 1 << 2,
            Any = ~0b0
        }
    }
}
