using System;
using System.IO;
using System.Text;

namespace ItemQualities.Serialization
{
    internal sealed class ReaderContext : IDisposable
    {
        public BinaryReader Reader { get; }

        public uint SerializedVersion { get; }

        private int _currentSharedBitIndex;
        private readonly int _sharedBitCount;
        private readonly byte[] _sharedBitsArray;

        public ReaderContext(Stream stream, Encoding encoding, bool leaveOpen)
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

    internal sealed class WriterContext : IDisposable
    {
        // TODO: Move this somewhere more appropriate
        public const uint CurrentVersion = 0;

        public BinaryWriter Writer { get; }

        private readonly Encoding _encoding;

        private int _sharedBitCount;
        private byte[] _sharedBitsArray = new byte[32];

        public WriterContext(Encoding encoding)
        {
            Writer = new BinaryWriter(new MemoryStream(), encoding);
        }

        public void WriteSharedBit(bool value)
        {
            if (value)
            {
                _sharedBitsArray[_sharedBitCount / 8] |= (byte)(1 << (_sharedBitCount % 8));
            }

            if (++_sharedBitCount / 8 > _sharedBitsArray.Length - 1)
            {
                Array.Resize(ref _sharedBitsArray, _sharedBitsArray.Length * 2);
                Log.Warning($"Reallocation of shared bits: new size: {_sharedBitsArray.Length}, consider increasing initial capacity");
            }
        }

        public void WriteTo(Stream stream)
        {
            // Write header
            using (BinaryWriter headerWriter = new BinaryWriter(stream, _encoding, true))
            {
                headerWriter.WritePackedUInt32(CurrentVersion);

                int sharedByteCount = HGMath.IntDivCeil(_sharedBitCount, 8);
                headerWriter.WritePackedUInt32((uint)_sharedBitCount);
                headerWriter.Write(_sharedBitsArray, 0, sharedByteCount);
            }

            long baseStreamPosition = Writer.BaseStream.Position;
            Writer.BaseStream.Seek(0, SeekOrigin.Begin);
            Writer.BaseStream.CopyTo(stream);
            Writer.BaseStream.Seek(baseStreamPosition, SeekOrigin.Begin);
        }

        public void Dispose()
        {
            Writer.Dispose();
        }
    }
}
