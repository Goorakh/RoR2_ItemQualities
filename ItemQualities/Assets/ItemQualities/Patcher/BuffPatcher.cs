using BepInEx.Logging;
using Mono.Cecil;
using System.Collections.Generic;

namespace ItemQualitiesPatcher
{
    public static class BuffPatcher
    {
        private static readonly LogWriter _log = new LogWriter();

        public static IEnumerable<string> TargetDLLs { get; } = new string[] { AssemblyNames.RoR2 };

        public static void Initialize()
        {
            _log.SetLogSource(Logger.CreateLogSource(nameof(BuffPatcher)));
        }

        public static void Patch(AssemblyDefinition assembly)
        {
            TypeDefinition characterBodyType = assembly.MainModule.GetType("RoR2.CharacterBody");
            if (characterBodyType == null)
            {
                _log.Error("Failed to find type: CharacterBody");
                return;
            }

            foreach (MethodDefinition method in characterBodyType.Methods)
            {
                if (method.Name == "GetBuffCount" || method.Name == "HasBuff")
                {
                    disableInlining(method);
                }
            }
        }

        private static void disableInlining(MethodDefinition method)
        {
            method.ImplAttributes &= ~MethodImplAttributes.AggressiveInlining;
            method.ImplAttributes |= MethodImplAttributes.NoInlining;

            _log.Debug($"Disabled inlining for method {method.FullName}");
        }
    }
}
