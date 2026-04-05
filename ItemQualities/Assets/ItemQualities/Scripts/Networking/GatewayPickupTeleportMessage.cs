using R2API.Networking.Interfaces;
using UnityEngine;
using UnityEngine.Networking;

namespace ItemQualities.Networking
{
    public sealed class GatewayPickupTeleportMessage : INetMessage
    {
        GameObject _pickupObject;

        public GatewayPickupTeleportMessage(GameObject pickupObject)
        {
            _pickupObject = pickupObject;
        }

        public GatewayPickupTeleportMessage()
        {
        }

        void ISerializableObject.Serialize(NetworkWriter writer)
        {
            writer.Write(_pickupObject);
        }

        void ISerializableObject.Deserialize(NetworkReader reader)
        {
            _pickupObject = reader.ReadGameObject();
        }

        void INetMessage.OnReceived()
        {
            if (_pickupObject && _pickupObject.TryGetComponent(out GatewayQualityPickupController pickupController))
            {
                pickupController.OnTeleportServer();
            }
        }
    }
}
