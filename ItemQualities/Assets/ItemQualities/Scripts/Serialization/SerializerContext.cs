using ItemQualities.SaveData;
using System;
using System.IO;
using System.Text;

namespace ItemQualities.Serialization
{
    internal sealed class SerializerContext : IDisposable
    {
        public BinaryWriter Writer { get; }

        private readonly Encoding _encoding;

        private int _sharedBitCount;
        private byte[] _sharedBitsArray = new byte[32];

        public SerializerContext()
            : this(Encoding.UTF8)
        {

        }

        public SerializerContext(Encoding encoding)
        {
            Writer = new BinaryWriter(new MemoryStream(), encoding);
            _encoding = encoding;
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
                headerWriter.WritePackedUInt32(SaveManager.SaveFileVersion);

                int sharedByteCount = HGMath.IntDivCeil(_sharedBitCount, 8);
                headerWriter.WritePackedUInt32((uint)_sharedBitCount);
                headerWriter.Write(_sharedBitsArray, 0, sharedByteCount);
            }

            // Copy body into output stream at current position
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
