using EntityStates;
using HG;
using ItemQualities.Caching;
using ItemQualities.Serialization;
using ItemQualities.Utilities.Extensions;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;
using RoR2;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;

using Path = System.IO.Path;

namespace ItemQualities
{
    internal static class EntityStatePatcher
    {
        public struct PatcherInfo
        {
            public readonly string Identifier;
            public readonly ILContext.Manipulator Manipulator;
            public Predicate<ILContext> ShouldApplyPredicate;

            public PatcherInfo(string identifier, ILContext.Manipulator manipulator) : this()
            {
                Identifier = identifier;
                Manipulator = manipulator;
            }
        }

        private sealed class PatcherInfoNameComparer : IComparer<PatcherInfo>
        {
            public static readonly PatcherInfoNameComparer Instance = new PatcherInfoNameComparer();

            public int Compare(PatcherInfo x, PatcherInfo y)
            {
                return StringComparer.Ordinal.Compare(x.Identifier, y.Identifier);
            }
        }

        private static readonly List<PatcherInfo> _patchers = new List<PatcherInfo>();

        public static void AddPatcher(in PatcherInfo patcherInfo)
        {
            if (string.IsNullOrEmpty(patcherInfo.Identifier))
            {
                throw new ArgumentException($"Patcher info must have an identifier", nameof(patcherInfo));
            }

            if (patcherInfo.Manipulator == null)
            {
                throw new ArgumentException($"Patcher info must provide an il manipulator", nameof(patcherInfo));
            }

            _patchers.Add(patcherInfo);

            if (RoR2Application.loadFinished)
            {
                executePatchers();
            }
            else if (_patchers.Count == 1)
            {
                RoR2Application.onLoad += onLoad;
            }
        }

        private static void onLoad()
        {
            executePatchers();
            _patchers.Clear();
            RoR2Application.onLoad -= onLoad;
        }

        private static void executePatchers()
        {
            string baseCacheDirectory = CacheFileHelper.GetCacheFolderPath("EntityStatePatcher");

            Stopwatch stopwatch = Stopwatch.StartNew();

            Dictionary<Assembly, HashSet<Type>> entityStateTypesByAssembly = new Dictionary<Assembly, HashSet<Type>>();

            int numStateTypes = 0;

            for (int i = 0; i < EntityStateCatalog.stateIndexToType.Length; i++)
            {
                Type stateType = EntityStateCatalog.stateIndexToType[i];
                while (stateType != null &&
                       typeof(EntityState).IsAssignableFrom(stateType) &&
                       entityStateTypesByAssembly.GetOrAddNew(stateType.Assembly).Add(stateType))
                {
                    numStateTypes++;
                    stateType = stateType.BaseType;
                }
            }

            entityStateTypesByAssembly.Remove(Assembly.GetExecutingAssembly());

            Log.Debug($"Discovered {numStateTypes} entity state types across {entityStateTypesByAssembly.Count} assemblies ({stopwatch.Elapsed.TotalMilliseconds:F0}ms)");
            stopwatch.Restart();

            int numAppliedHooks = 0;

            if (entityStateTypesByAssembly.Count > 0)
            {
                foreach ((Assembly assembly, HashSet<Type> entityStateTypes) in entityStateTypesByAssembly)
                {
                    foreach (PatcherInfo patcherInfo in _patchers)
                    {
                        string patcherCacheDirectory = Path.Combine(baseCacheDirectory, patcherInfo.Identifier);
                        string patcherCacheFilePath = Path.Combine(patcherCacheDirectory, assembly.FullName);

                        // Ensure cache directory exists
                        Directory.CreateDirectory(patcherCacheDirectory);

                        List<MethodInfo> patchMethods = new List<MethodInfo>();
                        List<MethodInfo> unverifiedMethods = new List<MethodInfo>();

                        bool cacheDirty = false;
                        if (!tryReadFromCache(assembly, patcherCacheFilePath, patcherInfo, patchMethods, unverifiedMethods))
                        {
                            collectPatchMethods(assembly, entityStateTypes, patcherInfo, patchMethods, unverifiedMethods);
                            cacheDirty = true;
                        }

                        for (int i = unverifiedMethods.Count - 1; i >= 0; i--)
                        {
                            MethodInfo unverifiedMethod = unverifiedMethods[i];
                            bool shouldApplyPatch = false;
                            try
                            {
                                _ = unverifiedMethod.DeclaringType;
                                _ = unverifiedMethod.GetMethodBody();

                                shouldApplyPatch = checkShouldPatchMethod(patcherInfo, unverifiedMethod);
                            }
                            catch
                            {
                                continue;
                            }

                            if (shouldApplyPatch)
                            {
                                patchMethods.Add(unverifiedMethod);
                            }

                            unverifiedMethods.RemoveAt(i);
                            cacheDirty = true;
                        }

                        for (int i = patchMethods.Count - 1; i >= 0; i--)
                        {
                            MethodInfo patchMethod = patchMethods[i];
                            try
                            {
                                _ = patchMethod.DeclaringType;
                                _ = patchMethod.GetMethodBody();
                            }
                            catch (Exception e)
                            {
                                Log.Warning_NoCallerPrefix($"Failed to resolve method {patchMethod.Name} from cache for {patcherInfo.Identifier} (in {assembly.FullName}): {e}");
                                patchMethods.RemoveAt(i);
                                unverifiedMethods.Add(patchMethod);
                                cacheDirty = true;
                                continue;
                            }

                            ILHook hook = null;
                            try
                            {
                                hook = new ILHook(patchMethod, patcherInfo.Manipulator, new ILHookConfig { ManualApply = true });
                                hook.Apply();
                            }
                            catch (Exception e)
                            {
                                Log.Warning($"Failed to apply patch {patcherInfo.Identifier} to {patchMethod.DeclaringType.FullName}.{patchMethod.Name} ({assembly.FullName}): {e.Message}");

                                hook?.Dispose();
                                hook = null;
                            }

                            if (hook != null)
                            {
                                numAppliedHooks++;
                            }
                        }

                        if (cacheDirty)
                        {
                            Log.Info($"Cache for {patcherInfo.Identifier} ({assembly.FullName}) was invalidated, writing new cache");
                            writeToCache(assembly, patcherCacheFilePath, patchMethods, unverifiedMethods);
                        }
                    }
                }
            }

            Log.Debug($"Applied {numAppliedHooks} entity state patches in {stopwatch.Elapsed.TotalMilliseconds:F0}ms");
        }

        private static bool checkShouldPatchMethod(in PatcherInfo patcherInfo, MethodBase method)
        {
            if (patcherInfo.ShouldApplyPredicate == null)
            {
                return true;
            }

            using DynamicMethodDefinition dmd = new DynamicMethodDefinition(method);
            using ILContext il = new ILContext(dmd.Definition);

            return patcherInfo.ShouldApplyPredicate(il);
        }

        private static void collectPatchMethods(Assembly assembly, HashSet<Type> entityStateTypes, in PatcherInfo patcherInfo, List<MethodInfo> destPatchMethods, List<MethodInfo> destUnverifiedMethods)
        {
            foreach (Type stateType in entityStateTypes)
            {
                foreach (MethodInfo method in stateType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                {
                    try
                    {
                        // The IsGenericMethod call sometimes causes a crash if accessed on a method where an assembly reference can't be resolved,
                        // the DeclaringType getter throws an exception instead, so do that first to catch it before trying to check IsGenericMethod
                        _ = method.DeclaringType;
                        if (method.IsGenericMethod || method.GetMethodBody() == null)
                            continue;

                        if (checkShouldPatchMethod(patcherInfo, method))
                        {
                            destPatchMethods.Add(method);
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Info_NoCallerPrefix($"Failed to check method {method.Name} ({assembly.FullName}) for patcher {patcherInfo.Identifier}: {e.Message}");
                        destUnverifiedMethods.Add(method);
                    }
                }
            }
        }

        private static bool tryReadFromCache(Assembly assembly, string cacheFilePath, in PatcherInfo patcherInfo, List<MethodInfo> destPatchMethods, List<MethodInfo> destUnverifiedMethods)
        {
            if (File.Exists(cacheFilePath))
            {
                try
                {
                    using (FileStream cacheFile = File.Open(cacheFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    using (BinaryReader cacheReader = new BinaryReader(cacheFile))
                    {
                        string cacheFileVersion = cacheReader.ReadString();
                        if (cacheFileVersion == ItemQualitiesPlugin.PluginVersion)
                        {
                            AssemblyCacheHeader assemblyCacheHeader = cacheReader.ReadAssemblyCacheHeader();
                            if (assemblyCacheHeader.VersionId == assembly.ManifestModule.ModuleVersionId)
                            {
                                {
                                    int patchMethodsCount = (int)cacheReader.ReadPackedUInt32();
                                    ListUtils.EnsureCapacity(destPatchMethods, destPatchMethods.Count + patchMethodsCount);
                                    for (int i = 0; i < patchMethodsCount; i++)
                                    {
                                        MethodInfo patchMethod = cacheReader.ReadMethodInfo(assembly);
                                        if (patchMethod != null)
                                        {
                                            destPatchMethods.Add(patchMethod);
                                        }
                                    }
                                }

                                {
                                    int unverifiedMethodsCount = (int)cacheReader.ReadPackedUInt32();
                                    ListUtils.EnsureCapacity(destUnverifiedMethods, destUnverifiedMethods.Count + unverifiedMethodsCount);
                                    for (int i = 0; i < unverifiedMethodsCount; i++)
                                    {
                                        MethodInfo unverifiedMethod = cacheReader.ReadMethodInfo(assembly);
                                        if (unverifiedMethod != null)
                                        {
                                            destUnverifiedMethods.Add(unverifiedMethod);
                                        }
                                    }
                                }

                                return true;
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Log.Error_NoCallerPrefix($"Failed to read from assembly cache at {cacheFilePath} for {patcherInfo.Identifier}, cache will be invalidated: {e}");
                }

                Log.Info($"Cache for {patcherInfo.Identifier} (assembly {assembly.FullName}) is out of date, it will be regenerated");
                File.Delete(cacheFilePath);
            }

            return false;
        }

        private static void writeToCache(Assembly assembly, string cacheFilePath, List<MethodInfo> patchMethods, List<MethodInfo> unverifiedMethods)
        {
            using FileStream cacheFile = File.Open(cacheFilePath, FileMode.Create, FileAccess.Write, FileShare.None);
            using BinaryWriter cacheWriter = new BinaryWriter(cacheFile);

            cacheWriter.Write(ItemQualitiesPlugin.PluginVersion);

            AssemblyCacheHeader assemblyCacheHeader = AssemblyCacheHeader.FromAssembly(assembly);
            cacheWriter.WriteAssemblyCacheHeader(assemblyCacheHeader);

            cacheWriter.WritePackedUInt32((uint)patchMethods.Count);
            foreach (MethodInfo patchMethod in patchMethods)
            {
                cacheWriter.WriteMember(patchMethod);
            }

            cacheWriter.WritePackedUInt32((uint)unverifiedMethods.Count);
            foreach (MethodInfo unverifiedMethod in unverifiedMethods)
            {
                cacheWriter.WriteMember(unverifiedMethod);
            }
        }

        private sealed class ManipulatorGroup
        {
            public readonly ILContext.Manipulator[] Manipulators;

            public ManipulatorGroup(ILContext.Manipulator[] manipulators)
            {
                Manipulators = manipulators;
            }

            public ILContext.Manipulator GetCombinedManipulator()
            {
                if (Manipulators.Length == 0)
                {
                    return null;
                }
                else if (Manipulators.Length == 1)
                {
                    return Manipulators[0];
                }

                return il =>
                {
                    foreach (ILContext.Manipulator manipulator in Manipulators)
                    {
                        manipulator(il);
                    }
                };
            }
        }
    }
}
