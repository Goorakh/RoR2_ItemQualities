using System;
using System.IO;
using System.Text;

namespace ItemQualities.Serialization
{
    internal sealed class DeserializerContext : IDisposable
    {
        public BinaryReader Reader { get; }

        /// <summary>
        /// The version of the save file format we are deserializing, current version is <see cref="SaveData.SaveManager.SaveFileVersion"/>
        /// </summary>
        public uint SerializedVersion { get; }

        private int _currentSharedBitIndex;
        private readonly int _sharedBitCount;
        private readonly byte[] _sharedBitsArray;

        public int SharedBitCount => _sharedBitCount;

        public DeserializerContext(Stream stream)
            : this(stream, Encoding.UTF8, true)
        {
        }

        public DeserializerContext(Stream stream, Encoding encoding, bool leaveOpen)
        {
            Reader = new BinaryReader(stream, encoding, leaveOpen);

            // Read header
            SerializedVersion = Reader.ReadPackedUInt32();

            _sharedBitCount = (int)Reader.ReadPackedUInt32();
            int sharedByteCount = HGMath.IntDivCeil(_sharedBitCount, 8);
            _sharedBitsArray = new byte[sharedByteCount];
            Reader.Read(_sharedBitsArray, 0, sharedByteCount);
        }

        public bool ReadSharedBit()
        {
            if (_currentSharedBitIndex >= _sharedBitCount)
            {
                throw new IndexOutOfRangeException($"Too many shared bits requested from buffer. Requesting index {_currentSharedBitIndex}, max {_sharedBitCount - 1}");
            }

            bool bitValue = (_sharedBitsArray[_currentSharedBitIndex / 8] & (byte)(1 << (_currentSharedBitIndex % 8))) != 0;
            _currentSharedBitIndex++;
            return bitValue;
        }

        public void Dispose()
        {
            Reader.Dispose();
        }
    }
}
