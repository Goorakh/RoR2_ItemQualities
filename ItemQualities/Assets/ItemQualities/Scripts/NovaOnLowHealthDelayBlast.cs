using RoR2;
using UnityEngine.Networking;

namespace ItemQualities
{
    public sealed class NovaOnLowHealthDelayBlast : NetworkBehaviour
    {
        private const uint dirtyBit = 1 << 0;

        private ProcChainMask _procChainMask;
        public ProcChainMask procChainMask
        {
            get => _procChainMask;
            [Server]
            set
            {
                _procChainMask = value;
                SetDirtyBit(dirtyBit);
            }
        }

        private float _procCoefficient;
        public float procCoefficient
        {
            get => _procCoefficient;
            [Server]
            set
            {
                _procCoefficient = value;
                SetDirtyBit(dirtyBit);
            }
        }

        public override bool OnSerialize(NetworkWriter writer, bool initialState)
        {
            writer.Write(_procChainMask);
            writer.Write(_procCoefficient);
            return true;
        }

        public override void OnDeserialize(NetworkReader reader, bool initialState)
        {
            _procChainMask = reader.ReadProcChainMask();
            _procCoefficient = reader.ReadSingle();
        }
    }
}
