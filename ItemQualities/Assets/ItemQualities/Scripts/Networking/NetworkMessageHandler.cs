using R2API.Networking;
using RoR2;

namespace ItemQualities.Networking
{
    internal static class NetworkMessageHandler
    {
        [InitDuringStartupPhase(GameInitPhase.PreFrame)]
        private static void Init()
        {
            NetworkingAPI.RegisterMessageType<GatewayPickupTeleportMessage>();
            NetworkingAPI.RegisterMessageType<SetBuffCountMessage>();
        }
    }
}
