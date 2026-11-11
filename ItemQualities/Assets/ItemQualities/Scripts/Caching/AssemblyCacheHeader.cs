using ItemQualities.Caching;
using System;
using System.IO;
using System.Reflection;

namespace ItemQualities.Caching
{
    internal readonly struct AssemblyCacheHeader
    {
        public readonly Guid VersionId;

        public AssemblyCacheHeader(Guid versionId)
        {
            VersionId = versionId;
        }

        public static AssemblyCacheHeader FromAssembly(Assembly assembly)
        {
            Guid versionId = assembly.ManifestModule.ModuleVersionId;
            return new AssemblyCacheHeader(versionId);
        }
    }
}

namespace ItemQualities.Serialization
{
    internal partial class SerializationExtensions
    {
        public static void WriteAssemblyCacheHeader(this BinaryWriter writer, in AssemblyCacheHeader assemblyCacheHeader)
        {
            writer.WriteGuid(assemblyCacheHeader.VersionId);
        }

        public static AssemblyCacheHeader ReadAssemblyCacheHeader(this BinaryReader reader)
        {
            Guid versionId = reader.ReadGuid();
            return new AssemblyCacheHeader(versionId);
        }
    }
}
