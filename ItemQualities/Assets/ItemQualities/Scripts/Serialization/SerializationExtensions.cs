using RoR2;
using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace ItemQualities.Serialization
{
    internal static class SerializationExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Read<T>(this DeserializerContext context)
            where T : IBinarySerializable, new()
        {
            T value = new T();
            value.Deserialize(context);
            return value;
        }

        public static void Write(this SerializerContext context, in NetworkUserId networkUserId)
        {
            BinaryWriter writer = context.Writer;

            bool isStringId = networkUserId.strValue != null;
            context.WriteSharedBit(isStringId);
            if (isStringId)
            {
                writer.Write(networkUserId.strValue);
            }
            else
            {
                writer.WritePackedUInt64(networkUserId.value);
            }

            writer.Write(networkUserId.subId);
        }

        public static NetworkUserId ReadNetworkUserId(this DeserializerContext context)
        {
            BinaryReader reader = context.Reader;

            NetworkUserId networkUserId = new NetworkUserId();

            bool isStringId = context.ReadSharedBit();
            if (isStringId)
            {
                networkUserId.strValue = reader.ReadString();
                networkUserId.value = 0;
            }
            else
            {
                networkUserId.strValue = null;
                networkUserId.value = reader.ReadPackedUInt64();
            }

            networkUserId.subId = reader.ReadByte();

            return networkUserId;
        }

        public static void WriteArray<T>(this SerializerContext context, T[] array)
            where T : IBinarySerializable
        {
            if (array == null)
            {
                context.WritePackedUInt32(0);
                return;
            }

            context.WritePackedUInt32((uint)array.Length);
            foreach (T item in array)
            {
                item.Serialize(context);
            }
        }

        public static T[] ReadArray<T>(this DeserializerContext context)
            where T : IBinarySerializable, new()
        {
            uint length = context.ReadPackedUInt32();
            if (length == 0)
            {
                return Array.Empty<T>();
            }

            T[] array = new T[length];
            for (int i = 0; i < length; i++)
            {
                array[i] = context.Read<T>();
            }

            return array;
        }

        #region Packed Int32
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WritePackedIndex32(this SerializerContext context, int value) => context.Writer.WritePackedUInt32((uint)(value + 1));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ReadPackedIndex32(this DeserializerContext context) => (int)context.Reader.ReadPackedUInt32() - 1;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WritePackedIndex32(this BinaryWriter writer, int value) => writer.WritePackedUInt32((uint)(value + 1));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ReadPackedIndex32(this BinaryReader reader) => (int)reader.ReadPackedUInt32() - 1;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WritePackedUInt32(this SerializerContext context, uint value) => context.Writer.WritePackedUInt32(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ReadPackedUInt32(this DeserializerContext context) => context.Reader.ReadPackedUInt32();

        /// <summary>
        /// <see href="https://sqlite.org/src4/doc/trunk/www/varint.wiki"/>
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="value"></param>
        public static void WritePackedUInt32(this BinaryWriter writer, uint value)
        {
            // Let the input value be V.
            switch (value)
            {
                case <= 240:
                    // If V<=240 then output a single by A0 equal to V.
                    writer.Write((byte)value);
                    break;
                case <= 2287:
                    // If V<=2287 then output A0 as (V-240)/256 + 241 and A1 as (V-240)%256.
                    writer.Write((byte)(((value - 240) / 256) + 241));
                    writer.Write((byte)((value - 240) % 256));
                    break;
                case <= 67823:
                    // If V<=67823 then output A0 as 249, A1 as (V-2288)/256, and A2 as (V-2288)%256.
                    writer.Write((byte)249);
                    writer.Write((byte)((value - 2288) / 256));
                    writer.Write((byte)((value - 2288) % 256));
                    break;
                case <= 16777215:
                    // If V<=16777215 then output A0 as 250 and A1 through A3 as a big-endian 3-byte integer.
                    writer.Write((byte)250);
                    writer.Write((byte)(value & 0xFF));
                    writer.Write((byte)((value >> 8) & 0xFF));
                    writer.Write((byte)((value >> 16) & 0xFF));
                    break;
                default:
                    // If V<=4294967295 (uint.MaxValue) then output A0 as 251 and A1..A4 as a big-ending 4-byte integer.
                    writer.Write((byte)251);
                    writer.Write((byte)(value & 0xFF));
                    writer.Write((byte)((value >> 8) & 0xFF));
                    writer.Write((byte)((value >> 16) & 0xFF));
                    writer.Write((byte)((value >> 24) & 0xFF));
                    break;
            }
        }

        /// <summary>
        /// <see href="https://sqlite.org/src4/doc/trunk/www/varint.wiki"/>
        /// </summary>
        /// <param name="reader"></param>
        /// <returns></returns>
        public static uint ReadPackedUInt32(this BinaryReader reader)
        {
            byte a0 = reader.ReadByte();
            if (a0 <= 240)
            {
                // If A0 is between 0 and 240 inclusive, then the result is the value of A0.
                return a0;
            }

            byte a1 = reader.ReadByte();
            if (a0 <= 248)
            {
                // If A0 is between 241 and 248 inclusive, then the result is 240+256*(A0-241)+A1.
                return (uint)(240 + (256 * (a0 - 241)) + a1);
            }

            byte a2 = reader.ReadByte();
            if (a0 == 249)
            {
                // If A0 is 249 then the result is 2288+256*A1+A2.
                return (uint)(2288 + (256 * a1) + a2);
            }

            byte a3 = reader.ReadByte();
            if (a0 == 250)
            {
                // If A0 is 250 then the result is A1..A3 as a 3-byte big-ending integer.
                return (uint)(a1 | (a2 << 8) | (a3 << 16));
            }

            byte a4 = reader.ReadByte();

            // If A0 is 251 then the result is A1..A4 as a 4-byte big-ending integer.
            return (uint)(a1 | (a2 << 8) | (a3 << 16) | (a4 << 24));
        }
        #endregion

        #region Packed Int64
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WritePackedUInt64(this SerializerContext context, ulong value) => context.Writer.WritePackedUInt64(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong ReadPackedUInt64(this DeserializerContext context) => context.Reader.ReadPackedUInt64();

        /// <summary>
        /// <see href="https://sqlite.org/src4/doc/trunk/www/varint.wiki"/>
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="value"></param>
        public static void WritePackedUInt64(this BinaryWriter writer, ulong value)
        {
            // Let the input value be V.
            switch (value)
            {
                case <= 240:
                    // If V<=240 then output a single by A0 equal to V.
                    writer.Write((byte)value);
                    break;
                case <= 2287:
                    // If V<=2287 then output A0 as (V-240)/256 + 241 and A1 as (V-240)%256.
                    writer.Write((byte)(((value - 240) / 256) + 241));
                    writer.Write((byte)((value - 240) % 256));
                    break;
                case <= 67823:
                    // If V<=67823 then output A0 as 249, A1 as (V-2288)/256, and A2 as (V-2288)%256.
                    writer.Write((byte)249);
                    writer.Write((byte)((value - 2288) / 256));
                    writer.Write((byte)((value - 2288) % 256));
                    break;
                case <= 16777215:
                    // If V<=16777215 then output A0 as 250 and A1 through A3 as a big-endian 3-byte integer.
                    writer.Write((byte)250);
                    writer.Write((byte)(value & 0xFF));
                    writer.Write((byte)((value >> 8) & 0xFF));
                    writer.Write((byte)((value >> 16) & 0xFF));
                    break;
                case <= 4294967295:
                    // If V<=4294967295 then output A0 as 251 and A1..A4 as a big-ending 4-byte integer.
                    writer.Write((byte)251);
                    writer.Write((byte)(value & 0xFF));
                    writer.Write((byte)((value >> 8) & 0xFF));
                    writer.Write((byte)((value >> 16) & 0xFF));
                    writer.Write((byte)((value >> 24) & 0xFF));
                    break;
                case <= 1099511627775:
                    // If V<=1099511627775 then output A0 as 252 and A1..A5 as a big-ending 5-byte integer.
                    writer.Write((byte)252);
                    writer.Write((byte)(value & 0xFF));
                    writer.Write((byte)((value >> 8) & 0xFF));
                    writer.Write((byte)((value >> 16) & 0xFF));
                    writer.Write((byte)((value >> 24) & 0xFF));
                    writer.Write((byte)((value >> 32) & 0xFF));
                    break;
                case <= 281474976710655:
                    // If V<=281474976710655 then output A0 as 253 and A1..A6 as a big-ending 6-byte integer.
                    writer.Write((byte)253);
                    writer.Write((byte)(value & 0xFF));
                    writer.Write((byte)((value >> 8) & 0xFF));
                    writer.Write((byte)((value >> 16) & 0xFF));
                    writer.Write((byte)((value >> 24) & 0xFF));
                    writer.Write((byte)((value >> 32) & 0xFF));
                    writer.Write((byte)((value >> 40) & 0xFF));
                    break;
                case <= 72057594037927935:
                    // If V<=72057594037927935 then output A0 as 254 and A1..A7 as a big-ending 7-byte integer.
                    writer.Write((byte)254);
                    writer.Write((byte)(value & 0xFF));
                    writer.Write((byte)((value >> 8) & 0xFF));
                    writer.Write((byte)((value >> 16) & 0xFF));
                    writer.Write((byte)((value >> 24) & 0xFF));
                    writer.Write((byte)((value >> 32) & 0xFF));
                    writer.Write((byte)((value >> 40) & 0xFF));
                    writer.Write((byte)((value >> 48) & 0xFF));
                    break;
                default:
                    // Otherwise then output A0 as 255 and A1..A8 as a big-ending 8-byte integer.
                    writer.Write((byte)255);
                    writer.Write((byte)(value & 0xFF));
                    writer.Write((byte)((value >> 8) & 0xFF));
                    writer.Write((byte)((value >> 16) & 0xFF));
                    writer.Write((byte)((value >> 24) & 0xFF));
                    writer.Write((byte)((value >> 32) & 0xFF));
                    writer.Write((byte)((value >> 40) & 0xFF));
                    writer.Write((byte)((value >> 48) & 0xFF));
                    writer.Write((byte)((value >> 56) & 0xFF));
                    break;
            }
        }

        /// <summary>
        /// <see href="https://sqlite.org/src4/doc/trunk/www/varint.wiki"/>
        /// </summary>
        /// <param name="reader"></param>
        /// <returns></returns>
        public static ulong ReadPackedUInt64(this BinaryReader reader)
        {
            byte a0 = reader.ReadByte();
            if (a0 <= 240)
            {
                // If A0 is between 0 and 240 inclusive, then the result is the value of A0.
                return a0;
            }

            byte a1 = reader.ReadByte();
            if (a0 <= 248)
            {
                // If A0 is between 241 and 248 inclusive, then the result is 240+256*(A0-241)+A1.
                return (ulong)(240 + (256 * (a0 - 241)) + a1);
            }

            byte a2 = reader.ReadByte();
            if (a0 == 249)
            {
                // If A0 is 249 then the result is 2288+256*A1+A2.
                return (ulong)(2288 + (256 * a1) + a2);
            }

            byte a3 = reader.ReadByte();
            if (a0 == 250)
            {
                // If A0 is 250 then the result is A1..A3 as a 3-byte big-ending integer.
                return (ulong)(a1 | (a2 << 8) | (a3 << 16));
            }

            byte a4 = reader.ReadByte();
            if (a0 == 251)
            {
                // If A0 is 251 then the result is A1..A4 as a 4-byte big-ending integer.
                return (ulong)(a1 | (a2 << 8) | (a3 << 16) | (a4 << 24));
            }

            byte a5 = reader.ReadByte();
            if (a0 == 252)
            {
                // If A0 is 252 then the result is A1..A5 as a 5-byte big-ending integer.
                return a1 | ((ulong)a2 << 8) | ((ulong)a3 << 16) | ((ulong)a4 << 24) | ((ulong)a5 << 32);
            }

            byte a6 = reader.ReadByte();
            if (a0 == 253)
            {
                // If A0 is 253 then the result is A1..A6 as a 6-byte big-ending integer.
                return a1 | ((ulong)a2 << 8) | ((ulong)a3 << 16) | ((ulong)a4 << 24) | ((ulong)a5 << 32) | ((ulong)a6 << 40);
            }

            byte a7 = reader.ReadByte();
            if (a0 == 254)
            {
                // If A0 is 254 then the result is A1..A7 as a 7-byte big-ending integer.
                return a1 | ((ulong)a2 << 8) | ((ulong)a3 << 16) | ((ulong)a4 << 24) | ((ulong)a5 << 32) | ((ulong)a6 << 40) | ((ulong)a7 << 48);
            }

            byte a8 = reader.ReadByte();

            // If A0 is 255 then the result is A1..A8 as a 8-byte big-ending integer.
            return a1 | ((ulong)a2 << 8) | ((ulong)a3 << 16) | ((ulong)a4 << 24) | ((ulong)a5 << 32) | ((ulong)a6 << 40) | ((ulong)a7 << 48) | ((ulong)a8 << 56);
        }
        #endregion
    }
}
