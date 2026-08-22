using HG;
using HG.Reflection;
using ItemQualities.Utilities.Extensions;
using RoR2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Networking;

namespace ItemQualities.Buffs
{
    public abstract class QualityBuffBodyBehavior : MonoBehaviour
    {
        private static BuffIndex[] _allBuffsToCheck = Array.Empty<BuffIndex>();

        private static readonly QualityGroupBehaviorCollection[] _behaviorCollectionsLookup = new QualityGroupBehaviorCollection[(int)QualityBuffBehaviorUsageFlags.All];

        private static readonly Dictionary<UnityObjectWrapperKey<CharacterBody>, BodyBehaviorInfo> _bodyQualityBehaviorInfoLookup = new Dictionary<UnityObjectWrapperKey<CharacterBody>, BodyBehaviorInfo>();

        private static CharacterBody _earlyAssignmentBody;
        private static BuffQualityCounts _earlyAssignmentStacks;

        public CharacterBody Body { get; private set; }

        private BuffQualityCounts _stacks;
        public ref readonly BuffQualityCounts Stacks
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref _stacks;
        }

        protected virtual void Awake()
        {
            Body = _earlyAssignmentBody;
            _earlyAssignmentBody = null;

            _stacks = _earlyAssignmentStacks;
            _earlyAssignmentStacks = default;
        }

        protected virtual void OnStacksChanged()
        {
        }

        [SystemInitializer(typeof(QualityCatalog))]
        private static void Init()
        {
            Span<List<QualityGroupBehaviorInfo>> qualityGroupBehaviorsByUsageLookup = new List<QualityGroupBehaviorInfo>[(int)QualityBuffBehaviorUsageFlags.All];
            foreach (ref List<QualityGroupBehaviorInfo> qualityGroupBehaviors in qualityGroupBehaviorsByUsageLookup)
            {
                qualityGroupBehaviors = new List<QualityGroupBehaviorInfo>();
            }

            foreach (BuffGroupAssociationAttribute buffGroupAttribute in SearchableAttribute.GetInstances<BuffGroupAssociationAttribute>()
                                                                                            .OfType<BuffGroupAssociationAttribute>())
            {
                MethodInfo getBuffGroupMethod = buffGroupAttribute.target;
                if (getBuffGroupMethod == null)
                {
                    Log.Error("Null target method for buff group attribute.");
                    continue;
                }

                QualityBuffBehaviorUsageFlags buffBehaviorUsage = buffGroupAttribute.Usage & QualityBuffBehaviorUsageFlags.All;

                Type qualityBuffBehaviorType = null;
                try
                {
                    qualityBuffBehaviorType = getBuffGroupMethod.DeclaringType;

                    if (buffBehaviorUsage == QualityBuffBehaviorUsageFlags.None)
                    {
                        Log.Error($"{nameof(BuffGroupAssociationAttribute)} method ({qualityBuffBehaviorType.FullName}.{getBuffGroupMethod.Name}) has no usage defined.");
                        continue;
                    }

                    if (!typeof(QualityBuffBodyBehavior).IsAssignableFrom(qualityBuffBehaviorType))
                    {
                        Log.Error($"{nameof(BuffGroupAssociationAttribute)} method ({qualityBuffBehaviorType.FullName}.{getBuffGroupMethod.Name}) must be declared in a type that inherits from {nameof(QualityBuffBodyBehavior)}. Found type: {qualityBuffBehaviorType.FullName}");
                        continue;
                    }

                    if (!getBuffGroupMethod.IsStatic)
                    {
                        Log.Error($"{nameof(BuffGroupAssociationAttribute)} method ({qualityBuffBehaviorType.FullName}.{getBuffGroupMethod.Name}) must be static");
                        continue;
                    }

                    if (getBuffGroupMethod.ReturnType != typeof(BuffQualityGroup))
                    {
                        Log.Error($"{nameof(BuffGroupAssociationAttribute)} method ({qualityBuffBehaviorType.FullName}.{getBuffGroupMethod.Name}) must return {nameof(BuffQualityGroup)}. Found return type: {getBuffGroupMethod.ReturnType.FullName}");
                        continue;
                    }

                    if (getBuffGroupMethod.GetParameters().Length != 0)
                    {
                        Log.Error($"{nameof(BuffGroupAssociationAttribute)} method ({qualityBuffBehaviorType.FullName}.{getBuffGroupMethod.Name}) cannot have parameters");
                        continue;
                    }

                    BuffQualityGroup targetBuffGroup = getBuffGroupMethod.Invoke(null, Array.Empty<object>()) as BuffQualityGroup;
                    if (!targetBuffGroup)
                    {
                        Log.Error($"{nameof(BuffGroupAssociationAttribute)} method ({qualityBuffBehaviorType.FullName}.{getBuffGroupMethod.Name}) returned null");
                        continue;
                    }

                    if (targetBuffGroup.GroupIndex == BuffQualityGroupIndex.Invalid)
                    {
                        Log.Error($"{nameof(BuffGroupAssociationAttribute)} method ({qualityBuffBehaviorType.FullName}.{getBuffGroupMethod.Name}) returned a group that is not registered in {nameof(QualityCatalog)}.");
                        continue;
                    }

                    // Place this behavior in the lists such that we can quickly find it again just by using the full flags value as an index into an array
                    for (QualityBuffBehaviorUsageFlags usageFlags = (QualityBuffBehaviorUsageFlags)1; usageFlags <= QualityBuffBehaviorUsageFlags.All; usageFlags++)
                    {
                        if ((usageFlags & buffBehaviorUsage) != 0)
                        {
                            List<QualityGroupBehaviorInfo> qualityGroupBehaviors = qualityGroupBehaviorsByUsageLookup[(int)usageFlags - 1];
                            qualityGroupBehaviors.Add(new QualityGroupBehaviorInfo(targetBuffGroup.GroupIndex, qualityBuffBehaviorType));
                        }
                    }
                }
                catch (Exception e)
                {
                    Log.Error_NoCallerPrefix($"Failed to register quality buff behavior for {qualityBuffBehaviorType?.FullName ?? "[UNRESOLVED TYPE]"} ({getBuffGroupMethod.Name}): {e}");
                }
            }

            Dictionary<int, FixedSizeArrayPool<QualityBuffBodyBehavior>> behaviorArrayPoolBySizeCache = new Dictionary<int, FixedSizeArrayPool<QualityBuffBodyBehavior>>(qualityGroupBehaviorsByUsageLookup.Length);

            HashSet<BuffIndex> allBuffsToCheck = new HashSet<BuffIndex>(BuffCatalog.buffCount);

            int numRegisteredBehaviors = 0;

            for (int i = 0; i < qualityGroupBehaviorsByUsageLookup.Length; i++)
            {
                QualityBuffBehaviorUsageFlags usageFlags = (QualityBuffBehaviorUsageFlags)(i + 1);

                List<QualityGroupBehaviorInfo> qualityGroupBehaviorsList = qualityGroupBehaviorsByUsageLookup[i];
                QualityGroupBehaviorInfo[] qualityGroupBehaviors = qualityGroupBehaviorsList.Count > 0 ? qualityGroupBehaviorsList.ToArray() : Array.Empty<QualityGroupBehaviorInfo>();

                FixedSizeArrayPool<QualityBuffBodyBehavior> behaviorsArrayPool = null;
                if (qualityGroupBehaviors.Length > 0)
                {
                    if (!behaviorArrayPoolBySizeCache.TryGetValue(qualityGroupBehaviors.Length, out behaviorsArrayPool))
                    {
                        behaviorsArrayPool = new FixedSizeArrayPool<QualityBuffBodyBehavior>(qualityGroupBehaviors.Length);
                        behaviorArrayPoolBySizeCache.Add(qualityGroupBehaviors.Length, behaviorsArrayPool);
                    }
                }

                _behaviorCollectionsLookup[i] = new QualityGroupBehaviorCollection(qualityGroupBehaviors, behaviorsArrayPool);
                numRegisteredBehaviors += qualityGroupBehaviors.Length;

                foreach (QualityGroupBehaviorInfo behaviorInfo in qualityGroupBehaviors)
                {
                    BuffQualityGroup buffGroup = QualityCatalog.GetBuffQualityGroup(behaviorInfo.BuffGroupIndex);

                    for (QualityTier qualityTier = 0; qualityTier < QualityTier.Count; qualityTier++)
                    {
                        BuffIndex buffIndex = buffGroup.GetBuffIndex(qualityTier);
                        if (buffIndex != BuffIndex.None)
                        {
                            allBuffsToCheck.Add(buffIndex);
                        }
                    }
                }

                Log.Debug($"({usageFlags}) behaviors: [{string.Join(", ", qualityGroupBehaviors.Select(b => b.BehaviorType.Name))}]");
            }

            Log.Debug($"Collected {numRegisteredBehaviors} quality buff behavior type(s)");

            if (numRegisteredBehaviors > 0)
            {
                CharacterBody.onBodyStartGlobal += onBodyStartGlobal;
                CharacterBody.onBodyDestroyGlobal += onBodyDestroyGlobal;
                BuffHooks.OnBodyBuffCountChangedGlobal += onBodyBuffCountChangedGlobal;
            }

            if (allBuffsToCheck.Count > 0)
            {
                _allBuffsToCheck = allBuffsToCheck.ToArray();
                Array.Sort(_allBuffsToCheck);
            }
        }

        private static QualityBuffBehaviorUsageFlags getBehaviorFlagsForBody(CharacterBody body)
        {
            QualityBuffBehaviorUsageFlags usageFlags = QualityBuffBehaviorUsageFlags.None;

            if (NetworkServer.active)
            {
                usageFlags |= QualityBuffBehaviorUsageFlags.Server;
            }

            if (NetworkClient.active)
            {
                usageFlags |= QualityBuffBehaviorUsageFlags.Client;
            }

            if (body && body.hasEffectiveAuthority)
            {
                usageFlags |= QualityBuffBehaviorUsageFlags.Authority;
            }

            return usageFlags;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int getBehaviorCollectionIndex(CharacterBody body)
        {
            return (int)getBehaviorFlagsForBody(body) - 1;
        }

        private static void onBodyStartGlobal(CharacterBody body)
        {
            if (!_bodyQualityBehaviorInfoLookup.ContainsKey(body))
            {
                int behaviorCollectionIndex = getBehaviorCollectionIndex(body);
                if (ArrayUtils.IsInBounds(_behaviorCollectionsLookup, behaviorCollectionIndex))
                {
                    ref readonly QualityGroupBehaviorCollection behaviorCollection = ref _behaviorCollectionsLookup[behaviorCollectionIndex];
                    if (behaviorCollection.BehaviorsArrayPool != null)
                    {
                        QualityBuffBodyBehavior[] qualityBuffBehaviors = behaviorCollection.BehaviorsArrayPool.Request();
                        BodyBehaviorInfo bodyBehaviorInfo = new BodyBehaviorInfo(qualityBuffBehaviors, behaviorCollectionIndex);

                        _bodyQualityBehaviorInfoLookup.Add(body, bodyBehaviorInfo);
                        refreshBodyQualityBehaviors(body, bodyBehaviorInfo);
                    }
                }
            }
        }

        private static void onBodyDestroyGlobal(CharacterBody body)
        {
            if (_bodyQualityBehaviorInfoLookup.Remove(body, out BodyBehaviorInfo behaviorInfo))
            {
                _behaviorCollectionsLookup[behaviorInfo.CollectionIndex].BehaviorsArrayPool?.Return(behaviorInfo.BehaviorComponents);
            }
        }

        private static void onBodyBuffCountChangedGlobal(CharacterBody body, BuffIndex buffIndex, int newCount)
        {
            if (Array.BinarySearch(_allBuffsToCheck, buffIndex) >= 0 &&
                _bodyQualityBehaviorInfoLookup.TryGetValue(body, out BodyBehaviorInfo behaviorInfo))
            {
                refreshBodyQualityBehaviors(body, behaviorInfo);
            }
        }

        private static void refreshBodyQualityBehaviors(CharacterBody body, in BodyBehaviorInfo bodyBehaviorInfo)
        {
            if (body)
            {
                ref readonly QualityGroupBehaviorCollection behaviorCollection = ref _behaviorCollectionsLookup[bodyBehaviorInfo.CollectionIndex];

                for (int i = 0; i < behaviorCollection.Behaviors.Length; i++)
                {
                    ref readonly QualityGroupBehaviorInfo behaviorInfo = ref behaviorCollection.Behaviors[i];

                    updateBuffStacks(body, ref bodyBehaviorInfo.BehaviorComponents[i], behaviorInfo.BehaviorType, body.GetBuffCounts(behaviorInfo.BuffGroupIndex));
                }
            }
            else
            {
                for (int i = 0; i < bodyBehaviorInfo.BehaviorComponents.Length; i++)
                {
                    ref QualityBuffBodyBehavior behavior = ref bodyBehaviorInfo.BehaviorComponents[i];
                    if (!ReferenceEquals(behavior, null))
                    {
                        Destroy(behavior);
                        behavior = null;
                    }
                }
            }
        }

        private static void updateBuffStacks(CharacterBody body, ref QualityBuffBodyBehavior buffBehavior, Type qualityBehaviorType, in BuffQualityCounts buffCounts)
        {
            bool hasBehavior = !ReferenceEquals(buffBehavior, null);
            bool shouldHaveBehavior = buffCounts.TotalQualityCount > 0;

            if (hasBehavior != shouldHaveBehavior)
            {
                if (shouldHaveBehavior)
                {
                    _earlyAssignmentBody = body;
                    _earlyAssignmentStacks = buffCounts;
                    try
                    {
                        buffBehavior = (QualityBuffBodyBehavior)body.gameObject.AddComponent(qualityBehaviorType);
                    }
                    finally
                    {
                        _earlyAssignmentBody = null;
                        _earlyAssignmentStacks = default;
                    }

                    buffBehavior.OnStacksChanged();

                    hasBehavior = true;
                }
                else
                {
                    Destroy(buffBehavior);
                    buffBehavior = null;

                    hasBehavior = false;
                }
            }
            else if (hasBehavior && buffBehavior._stacks != buffCounts)
            {
                buffBehavior._stacks = buffCounts;
                buffBehavior.OnStacksChanged();
            }
        }

        private readonly struct BodyBehaviorInfo
        {
            public readonly QualityBuffBodyBehavior[] BehaviorComponents;

            public readonly int CollectionIndex;

            public BodyBehaviorInfo(QualityBuffBodyBehavior[] behaviors, int collectionIndex)
            {
                BehaviorComponents = behaviors;
                CollectionIndex = collectionIndex;
            }
        }

        private readonly struct QualityGroupBehaviorInfo
        {
            public readonly BuffQualityGroupIndex BuffGroupIndex;

            public readonly Type BehaviorType;

            public QualityGroupBehaviorInfo(BuffQualityGroupIndex groupIndex, Type qualityBuffBehaviorType)
            {
                BuffGroupIndex = groupIndex;
                BehaviorType = qualityBuffBehaviorType;
            }
        }

        private readonly struct QualityGroupBehaviorCollection
        {
            public readonly QualityGroupBehaviorInfo[] Behaviors;

            public readonly FixedSizeArrayPool<QualityBuffBodyBehavior> BehaviorsArrayPool;

            public QualityGroupBehaviorCollection(QualityGroupBehaviorInfo[] buffGroups, FixedSizeArrayPool<QualityBuffBodyBehavior> behaviorsArrayPool)
            {
                Behaviors = buffGroups;
                BehaviorsArrayPool = behaviorsArrayPool;
            }
        }

        [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
        protected sealed class BuffGroupAssociationAttribute : SearchableAttribute
        {
            public new MethodInfo target => base.target as MethodInfo;

            public QualityBuffBehaviorUsageFlags Usage { get; }

            public BuffGroupAssociationAttribute(QualityBuffBehaviorUsageFlags usage)
            {
                Usage = usage;
            }
        }

        [Flags]
        public enum QualityBuffBehaviorUsageFlags : uint
        {
            None = 0,
            Server = 1 << 0,
            Client = 1 << 1,
            Authority = 1 << 2,
            All = Server | Client | Authority,
        }
    }
}
