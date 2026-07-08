using R2API.Networking.Interfaces;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace ItemQualities.Networking
{
    public sealed class SetBuffCountMessage : INetMessage
    {
        private CharacterBody _body;
        private BuffIndex _buffIndex = BuffIndex.None;
        private int _buffCount;

        public SetBuffCountMessage(CharacterBody body, BuffIndex buffIndex, int buffCount)
        {
            _body = body;
            _buffIndex = buffIndex;
            _buffCount = buffCount;
        }

        public SetBuffCountMessage()
        {
        }

        void ISerializableObject.Serialize(NetworkWriter writer)
        {
            writer.Write(_body.gameObject);
            writer.WriteBuffIndex(_buffIndex);
            writer.WritePackedUInt32((uint)_buffCount);
        }

        void ISerializableObject.Deserialize(NetworkReader reader)
        {
            GameObject bodyObject = reader.ReadGameObject();
            _body = bodyObject ? bodyObject.GetComponent<CharacterBody>() : null;
            _buffIndex = reader.ReadBuffIndex();
            _buffCount = (int)reader.ReadPackedUInt32();
        }

        void INetMessage.OnReceived()
        {
            _body.SetBuffCount(_buffIndex, _buffCount);
        }
    }
}
