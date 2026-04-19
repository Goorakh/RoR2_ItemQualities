using EntityStates;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;
using RoR2;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace ItemQualities
{
    static class EntityStatePatcher
    {
        public struct PatcherInfo
        {
            public Predicate<ILContext> ShouldApplyPredicate;
            public ILContext.Manipulator Manipulator;
        }

        static readonly List<PatcherInfo> _patchers = new List<PatcherInfo>();

        public static void AddPatcher(in PatcherInfo patcherInfo)
        {
            if (patcherInfo.Manipulator == null)
                throw new ArgumentException($"Patcher info must provide an il manipulator");

            if (_patchers.Count == 0)
            {
                RoR2Application.onLoad += onLoad;
            }

            _patchers.Add(patcherInfo);

            if (RoR2Application.loadFinished)
            {
                executePatchers();
            }
        }

        static void onLoad()
        {
            executePatchers();
            _patchers.Clear();
            RoR2Application.onLoad -= onLoad;
        }

        static void executePatchers()
        {
            HashSet<Type> allEntityStateTypes = new HashSet<Type>(EntityStateCatalog.stateIndexToType.Length);

            for (int i = 0; i < EntityStateCatalog.stateIndexToType.Length; i++)
            {
                Type stateType = EntityStateCatalog.stateIndexToType[i];
                while (stateType != null && typeof(EntityState).IsAssignableFrom(stateType) && allEntityStateTypes.Add(stateType))
                {
                    stateType = stateType.BaseType;
                }
            }

            int numAppliedHooks = 0;

            if (allEntityStateTypes.Count > 0)
            {
                List<ILContext.Manipulator> validManipulators = new List<ILContext.Manipulator>();

                foreach (Type stateType in allEntityStateTypes)
                {
                    try
                    {
                        if (stateType.Assembly == Assembly.GetExecutingAssembly())
                            continue;

                        foreach (MethodInfo method in stateType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                        {
                            ILHook hook = null;
                            try
                            {
                                // The IsGenericMethod call sometimes causes a crash if accessed on a method where an assembly reference can't be resolved,
                                // the DeclaringType getter throws an exception instead, so do that first to catch it before trying to check IsGenericMethod
                                _ = method.DeclaringType;
                                if (method.IsGenericMethod || method.GetMethodBody() == null)
                                    continue;

                                using DynamicMethodDefinition dmd = new DynamicMethodDefinition(method);
                                using ILContext il = new ILContext(dmd.Definition);

                                validManipulators.Clear();
                                foreach (PatcherInfo patcherInfo in _patchers)
                                {
                                    if (patcherInfo.ShouldApplyPredicate == null || patcherInfo.ShouldApplyPredicate(il))
                                    {
                                        validManipulators.Add(patcherInfo.Manipulator);
                                    }
                                }

                                if (validManipulators.Count > 0)
                                {
                                    ILContext.Manipulator manipulator;
                                    if (validManipulators.Count > 1)
                                    {
                                        ManipulatorGroup manipulatorGroup = new ManipulatorGroup(validManipulators.ToArray());
                                        manipulator = manipulatorGroup.GetCombinedManipulator();
                                    }
                                    else
                                    {
                                        manipulator = validManipulators[0];
                                    }

                                    hook = new ILHook(method, manipulator, new ILHookConfig { ManualApply = true });
                                    hook.Apply();
                                }
                            }
                            catch (Exception e)
                            {
                                Log.Warning($"Failed to apply attack radius hook to {method.DeclaringType.FullName}.{method.Name} ({stateType.Assembly.FullName}): {e.Message}");

                                hook?.Dispose();
                                hook = null;
                            }

                            if (hook != null)
                            {
                                numAppliedHooks++;
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Warning($"Failed to scan type for entity state patches: {stateType.FullName} ({stateType.Assembly.FullName}): {e.Message}");
                    }
                }
            }

            Log.Debug($"Applied {numAppliedHooks} entity state patch(es)");
        }

        sealed class ManipulatorGroup
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
