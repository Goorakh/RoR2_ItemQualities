using HG;
using HG.Coroutines;
using HG.Reflection;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ItemQualities.ContentManagement
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    internal sealed class ContentInitializerAttribute : SearchableAttribute
    {
        public new MethodInfo target => base.target as MethodInfo;

        public Type[] Dependencies { get; } = Array.Empty<Type>();

        public ContentInitializerAttribute()
        {
        }

        public ContentInitializerAttribute(params Type[] dependencies)
        {
            Dependencies = dependencies;
        }

        public static IEnumerator RunContentInitializers(ExtendedContentPack contentPack, IProgress<float> progressReceiver)
        {
            List<(IEnumerator coroutine, ReadableProgress<float> progress)> contentInitializersSequence = new();

            List<ParallelCoroutineGroup> contentInitializerGroups = new List<ParallelCoroutineGroup>();

            List<ContentInitializerAttribute> attributes = new List<ContentInitializerAttribute>();
            GetInstances(attributes);

            while (attributes.Count > 0)
            {
                bool anyAttributeAdded = false;

                for (int i = attributes.Count - 1; i >= 0; i--)
                {
                    ContentInitializerAttribute attribute = attributes[i];

                    MethodInfo method = attribute.target;
                    ParameterInfo[] methodParameters = method.GetParameters();
                    if (methodParameters.Length != 1 || methodParameters[0].ParameterType != typeof(ContentIntializerArgs))
                    {
                        Log.Error($"Invalid parameters for Content Initializer method {method.DeclaringType.FullName}.{method.Name}");
                        attributes.RemoveAt(i);
                        continue;
                    }

                    int highestGroupDependencyIndex = -1;
                    List<Type> initializerDependencies = attribute.Dependencies.ToList();
                    if (initializerDependencies.Count > 0)
                    {
                        for (int j = 0; j < contentInitializerGroups.Count; j++)
                        {
                            int removedDependencies = initializerDependencies.RemoveAll(contentInitializerGroups[j].InitializesType);
                            if (removedDependencies > 0)
                            {
                                highestGroupDependencyIndex = j;

                                if (initializerDependencies.Count == 0)
                                {
                                    break;
                                }
                            }
                        }
                    }

                    if (initializerDependencies.Count == 0)
                    {
                        ReadableProgress<float> contentInitializerProgress = new ReadableProgress<float>();
                        ContentIntializerArgs contentIntializerArgs = new ContentIntializerArgs(contentPack, contentInitializerProgress);

                        static IEnumerator runInitializerCoroutine(MethodInfo method, ContentIntializerArgs contentIntializerArgs)
                        {
                            object returnValue = method.Invoke(null, new object[] { contentIntializerArgs });
                            if (returnValue is IEnumerator enumerator)
                            {
                                yield return enumerator;
                            }
                            else if (returnValue is IEnumerable enumerable)
                            {
                                yield return enumerable.GetEnumerator();
                            }
                            else if (method.ReturnType != typeof(void))
                            {
                                throw new NotImplementedException($"Unhandled return value: {returnValue} ({method.ReturnType.FullName}) from method {method.DeclaringType.FullName}.{method.Name}");
                            }
                        }

                        ParallelCoroutineGroup initializerGroup;

                        int desiredGroupIndex = highestGroupDependencyIndex < 0 ? 0 : highestGroupDependencyIndex + 1;
                        if (desiredGroupIndex < contentInitializerGroups.Count)
                        {
                            initializerGroup = contentInitializerGroups[desiredGroupIndex];
                        }
                        else
                        {
                            ReadableProgress<float> groupProgress = new ReadableProgress<float>();
                            initializerGroup = new ParallelCoroutineGroup(groupProgress);
                            contentInitializerGroups.Add(initializerGroup);

                            contentInitializersSequence.Add((initializerGroup, groupProgress));
                        }

                        initializerGroup.Add(attribute, runInitializerCoroutine(method, contentIntializerArgs), contentInitializerProgress);

                        attributes.RemoveAt(i);

                        anyAttributeAdded = true;
                    }
                }

                if (!anyAttributeAdded)
                {
                    Log.Error($"Failed to find group for {attributes.Count} content initializer attribute(s)");
                    break;
                }
            }

            Log.Debug($"Content initializers separated into {contentInitializerGroups.Count} group(s):\n{string.Join("\n", contentInitializerGroups.Select(g => $"[{string.Join(", ", g.InitializedTypes.Select(t => t.FullName))}]"))}");

            using PartitionedProgress partitionedProgress = new PartitionedProgress(progressReceiver);
            IProgress<float>[] initializerGroupProgressReceivers = partitionedProgress.AddPartitions(contentInitializersSequence.Count);

            for (int i = 0; i < contentInitializersSequence.Count; i++)
            {
                (IEnumerator coroutine, ReadableProgress<float> progress) = contentInitializersSequence[i];

                while (coroutine.MoveNext())
                {
                    yield return coroutine.Current;
                    initializerGroupProgressReceivers[i].Report(progress.value);
                }

                initializerGroupProgressReceivers[i].Report(1f);
            }
        }

        class ParallelCoroutineGroup : IEnumerator
        {
            readonly HashSet<Type> _initializedTypes = new HashSet<Type>();
            readonly ParallelProgressCoroutine _combinedCoroutine;

            public readonly ReadableProgress<float> Progress;

            object IEnumerator.Current => ((IEnumerator)_combinedCoroutine).Current;

            public IReadOnlyCollection<Type> InitializedTypes => _initializedTypes;

            public ParallelCoroutineGroup(ReadableProgress<float> progressReceiver)
            {
                Progress = progressReceiver;
                _combinedCoroutine = new ParallelProgressCoroutine(Progress);
            }

            public bool InitializesType(Type type)
            {
                return _initializedTypes.Contains(type);
            }

            public void Add(ContentInitializerAttribute attribute, IEnumerator coroutine, ReadableProgress<float> coroutineProgressReceiver)
            {
                SetInitialized(attribute);
                _combinedCoroutine.Add(coroutine, coroutineProgressReceiver);
            }

            public void SetInitialized(ContentInitializerAttribute attribute)
            {
                _initializedTypes.Add(attribute.target.DeclaringType);
            }

            bool IEnumerator.MoveNext()
            {
                return ((IEnumerator)_combinedCoroutine).MoveNext();
            }

            void IEnumerator.Reset()
            {
                ((IEnumerator)_combinedCoroutine).Reset();
            }
        }
    }
}
