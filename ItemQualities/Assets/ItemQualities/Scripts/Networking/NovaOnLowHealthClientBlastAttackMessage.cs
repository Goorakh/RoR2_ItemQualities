using ItemQualities.Items;
using R2API.Networking.Interfaces;
using UnityEngine.Networking;

namespace ItemQualities.Networking
{
    internal sealed class NovaOnLowHealthClientBlastAttackMessage : INetMessage
    {
        private BlastAttackInfo _blastAttackInfo;

        public NovaOnLowHealthClientBlastAttackMessage(BlastAttackInfo blastAttackInfo)
        {
            _blastAttackInfo = blastAttackInfo;
        }

        public NovaOnLowHealthClientBlastAttackMessage()
        {
        }

        void ISerializableObject.Serialize(NetworkWriter writer)
        {
            BlastAttackInfo.Serialize(writer, _blastAttackInfo);
        }

        void ISerializableObject.Deserialize(NetworkReader reader)
        {
            _blastAttackInfo = BlastAttackInfo.Deserialize(reader);
        }

        void INetMessage.OnReceived()
        {
            NovaOnLowHealth.OnBlastAttackFireServer(_blastAttackInfo);
        }
    }
}
