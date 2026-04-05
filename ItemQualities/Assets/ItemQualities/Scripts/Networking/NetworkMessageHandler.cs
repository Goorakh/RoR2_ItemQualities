using R2API.Networking;
using RoR2;

namespace ItemQualities.Networking
{
    static class NetworkMessageHandler
    {
        [InitDuringStartupPhase(GameInitPhase.PreFrame)]
        static void Init()
        {
            NetworkingAPI.RegisterMessageType<GatewayPickupTeleportMessage>();
        }
    }
}
