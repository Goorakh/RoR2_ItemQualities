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

namespace ItemQualities.Items
{
    public abstract class QualityItemBodyBehavior : MonoBehaviour
    {
        readonly static QualityGroupBehaviorCollection[] _behaviorCollectionsLookup = new QualityGroupBehaviorCollection[(int)QualityItemBehaviorUsageFlags.All];

        static readonly Dictionary<UnityObjectWrapperKey<CharacterBody>, BodyBehaviorInfo> _bodyQualityBehaviorInfoLookup = new Dictionary<UnityObjectWrapperKey<CharacterBody>, BodyBehaviorInfo>();

        static CharacterBody _earlyAssignmentBody;
        public CharacterBody Body { get; private set; }

        ItemQualityCounts _stacks;
        public ItemQualityCounts Stacks => _stacks;

        protected virtual void Awake()
        {
            Body = _earlyAssignmentBody;
            _earlyAssignmentBody = null;
        }

        protected virtual void OnStacksChanged()
        {
        }

        [SystemInitializer(typeof(QualityCatalog))]
        static void Init()
        {
            Span<List<QualityGroupBehaviorInfo>> qualityGroupBehaviorsByUsageLookup = new List<QualityGroupBehaviorInfo>[(int)QualityItemBehaviorUsageFlags.All];
            foreach (ref List<QualityGroupBehaviorInfo> qualityGroupBehaviors in qualityGroupBehaviorsByUsageLookup)
            {
                qualityGroupBehaviors = new List<QualityGroupBehaviorInfo>();
            }

            foreach (ItemGroupAssociationAttribute itemGroupAttribute in SearchableAttribute.GetInstances<ItemGroupAssociationAttribute>()
                                                                                            .OfType<ItemGroupAssociationAttribute>())
            {
                MethodInfo getItemGroupMethod = itemGroupAttribute.target;
                if (getItemGroupMethod == null)
                {
                    Log.Error("Null target method for item group attribute.");
                    continue;
                }

                QualityItemBehaviorUsageFlags itemBehaviorUsage = itemGroupAttribute.Usage & QualityItemBehaviorUsageFlags.All;

                Type qualityItemBehaviorType = null;
                try
                {
                    qualityItemBehaviorType = getItemGroupMethod.DeclaringType;

                    if (itemBehaviorUsage == QualityItemBehaviorUsageFlags.None)
                    {
                        Log.Error($"{nameof(ItemGroupAssociationAttribute)} method ({qualityItemBehaviorType.FullName}.{getItemGroupMethod.Name}) has no usage defined.");
                        continue;
                    }

                    if (!typeof(QualityItemBodyBehavior).IsAssignableFrom(qualityItemBehaviorType))
                    {
                        Log.Error($"{nameof(ItemGroupAssociationAttribute)} method ({qualityItemBehaviorType.FullName}.{getItemGroupMethod.Name}) must be declared in a type that inherits from {nameof(QualityItemBodyBehavior)}. Found type: {qualityItemBehaviorType.FullName}");
                        continue;
                    }

                    if (!getItemGroupMethod.IsStatic)
                    {
                        Log.Error($"{nameof(ItemGroupAssociationAttribute)} method ({qualityItemBehaviorType.FullName}.{getItemGroupMethod.Name}) must be static");
                        continue;
                    }

                    if (getItemGroupMethod.ReturnType != typeof(ItemQualityGroup))
                    {
                        Log.Error($"{nameof(ItemGroupAssociationAttribute)} method ({qualityItemBehaviorType.FullName}.{getItemGroupMethod.Name}) must return {nameof(ItemQualityGroup)}. Found return type: {getItemGroupMethod.ReturnType.FullName}");
                        continue;
                    }

                    if (getItemGroupMethod.GetParameters().Length != 0)
                    {
                        Log.Error($"{nameof(ItemGroupAssociationAttribute)} method ({qualityItemBehaviorType.FullName}.{getItemGroupMethod.Name}) cannot have parameters");
                        continue;
                    }

                    ItemQualityGroup targetItemGroup = getItemGroupMethod.Invoke(null, Array.Empty<object>()) as ItemQualityGroup;
                    if (!targetItemGroup)
                    {
                        Log.Error($"{nameof(ItemGroupAssociationAttribute)} method ({qualityItemBehaviorType.FullName}.{getItemGroupMethod.Name}) returned null");
                        continue;
                    }

                    if (targetItemGroup.GroupIndex == ItemQualityGroupIndex.Invalid)
                    {
                        Log.Error($"{nameof(ItemGroupAssociationAttribute)} method ({qualityItemBehaviorType.FullName}.{getItemGroupMethod.Name}) returned a group that is not registered in {nameof(QualityCatalog)}.");
                        continue;
                    }

                    // Add to every set this usage is a subset of
                    for (QualityItemBehaviorUsageFlags usageFlags = (QualityItemBehaviorUsageFlags)1; usageFlags <= QualityItemBehaviorUsageFlags.All; usageFlags++)
                    {
                        if ((usageFlags & itemBehaviorUsage) == itemBehaviorUsage)
                        {
                            List<QualityGroupBehaviorInfo> qualityGroupBehaviors = qualityGroupBehaviorsByUsageLookup[(int)usageFlags - 1];
                            qualityGroupBehaviors.Add(new QualityGroupBehaviorInfo(targetItemGroup.GroupIndex, qualityItemBehaviorType));
                        }
                    }
                }
                catch (Exception e)
                {
                    Log.Error_NoCallerPrefix($"Failed to register quality item behavior for {qualityItemBehaviorType?.FullName ?? "[UNRESOLVED TYPE]"} ({getItemGroupMethod.Name}): {e}");
                }
            }

            Dictionary<int, FixedSizeArrayPool<QualityItemBodyBehavior>> behaviorArrayPoolBySizeCache = new Dictionary<int, FixedSizeArrayPool<QualityItemBodyBehavior>>(qualityGroupBehaviorsByUsageLookup.Length);

            int numRegisteredBehaviors = 0;

            for (int i = 0; i < qualityGroupBehaviorsByUsageLookup.Length; i++)
            {
                QualityItemBehaviorUsageFlags usageFlags = (QualityItemBehaviorUsageFlags)(i + 1);

                List<QualityGroupBehaviorInfo> qualityGroupBehaviorsList = qualityGroupBehaviorsByUsageLookup[i];
                QualityGroupBehaviorInfo[] qualityGroupBehaviors = qualityGroupBehaviorsList.Count > 0 ? qualityGroupBehaviorsList.ToArray() : Array.Empty<QualityGroupBehaviorInfo>();

                FixedSizeArrayPool<QualityItemBodyBehavior> behaviorsArrayPool = null;
                if (qualityGroupBehaviors.Length > 0)
                {
                    if (!behaviorArrayPoolBySizeCache.TryGetValue(qualityGroupBehaviors.Length, out behaviorsArrayPool))
                    {
                        behaviorsArrayPool = new FixedSizeArrayPool<QualityItemBodyBehavior>(qualityGroupBehaviors.Length);
                        behaviorArrayPoolBySizeCache.Add(qualityGroupBehaviors.Length, behaviorsArrayPool);
                    }
                }

                _behaviorCollectionsLookup[i] = new QualityGroupBehaviorCollection(qualityGroupBehaviors, behaviorsArrayPool);
                numRegisteredBehaviors += qualityGroupBehaviors.Length;
            }

            Log.Debug($"Collected {numRegisteredBehaviors} quality item behavior type(s)");

            if (numRegisteredBehaviors > 0)
            {
                CharacterBody.onBodyStartGlobal += onBodyStartGlobal;
                CharacterBody.onBodyDestroyGlobal += onBodyDestroyGlobal;
                CharacterBody.onBodyInventoryChangedGlobal += onBodyInventoryChangedGlobal;
            }
        }

        static QualityItemBehaviorUsageFlags getBehaviorFlagsForBody(CharacterBody body)
        {
            QualityItemBehaviorUsageFlags usageFlags = QualityItemBehaviorUsageFlags.None;

            if (NetworkServer.active)
            {
                usageFlags |= QualityItemBehaviorUsageFlags.Server;
            }

            if (NetworkClient.active)
            {
                usageFlags |= QualityItemBehaviorUsageFlags.Client;
            }

            if (body && body.hasEffectiveAuthority)
            {
                usageFlags |= QualityItemBehaviorUsageFlags.Authority;
            }

            return usageFlags;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int getBehaviorCollectionIndex(CharacterBody body)
        {
            return (int)getBehaviorFlagsForBody(body) - 1;
        }

        static void onBodyStartGlobal(CharacterBody body)
        {
            if (body.inventory && !_bodyQualityBehaviorInfoLookup.ContainsKey(body))
            {
                int behaviorCollectionIndex = getBehaviorCollectionIndex(body);
                if (ArrayUtils.IsInBounds(_behaviorCollectionsLookup, behaviorCollectionIndex))
                {
                    ref readonly QualityGroupBehaviorCollection behaviorCollection = ref _behaviorCollectionsLookup[behaviorCollectionIndex];
                    if (behaviorCollection.BehaviorsArrayPool != null)
                    {
                        QualityItemBodyBehavior[] qualityItemBehaviors = behaviorCollection.BehaviorsArrayPool.Request();
                        BodyBehaviorInfo bodyBehaviorInfo = new BodyBehaviorInfo(qualityItemBehaviors, behaviorCollectionIndex);

                        _bodyQualityBehaviorInfoLookup.Add(body, bodyBehaviorInfo);
                        refreshBodyQualityBehaviors(body, bodyBehaviorInfo);
                    }
                }
            }
        }

        static void onBodyDestroyGlobal(CharacterBody body)
        {
            if (_bodyQualityBehaviorInfoLookup.Remove(body, out BodyBehaviorInfo behaviorInfo))
            {
                _behaviorCollectionsLookup[behaviorInfo.CollectionIndex].BehaviorsArrayPool?.Return(behaviorInfo.BehaviorComponents);
            }
        }

        static void onBodyInventoryChangedGlobal(CharacterBody body)
        {
            if (_bodyQualityBehaviorInfoLookup.TryGetValue(body, out BodyBehaviorInfo behaviorInfo))
            {
                refreshBodyQualityBehaviors(body, behaviorInfo);
            }
        }

        static void refreshBodyQualityBehaviors(CharacterBody body, BodyBehaviorInfo bodyBehaviorInfo)
        {
            if (body.inventory)
            {
                ref readonly QualityGroupBehaviorCollection behaviorCollection = ref _behaviorCollectionsLookup[bodyBehaviorInfo.CollectionIndex];

                for (int i = 0; i < behaviorCollection.Behaviors.Length; i++)
                {
                    ref readonly QualityGroupBehaviorInfo behaviorInfo = ref behaviorCollection.Behaviors[i];

                    updateItemStacks(body, ref bodyBehaviorInfo.BehaviorComponents[i], behaviorInfo.BehaviorType, body.inventory.GetItemCountsEffective(behaviorInfo.ItemGroupIndex));
                }
            }
            else
            {
                for (int i = 0; i < bodyBehaviorInfo.BehaviorComponents.Length; i++)
                {
                    ref QualityItemBodyBehavior behavior = ref bodyBehaviorInfo.BehaviorComponents[i];
                    if (!ReferenceEquals(behavior, null))
                    {
                        Destroy(behavior);
                        behavior = null;
                    }
                }
            }
        }

        static void updateItemStacks(CharacterBody body, ref QualityItemBodyBehavior itemBehavior, Type qualityBehaviorType, in ItemQualityCounts itemCounts)
        {
            bool hasBehavior = !ReferenceEquals(itemBehavior, null);
            bool shouldHaveBehavior = itemCounts.TotalQualityCount > 0;

            if (hasBehavior != shouldHaveBehavior)
            {
                if (shouldHaveBehavior)
                {
                    _earlyAssignmentBody = body;
                    try
                    {
                        itemBehavior = (QualityItemBodyBehavior)body.gameObject.AddComponent(qualityBehaviorType);
                    }
                    finally
                    {
                        _earlyAssignmentBody = null;
                    }

                    hasBehavior = true;
                }
                else
                {
                    Destroy(itemBehavior);
                    itemBehavior = null;

                    hasBehavior = false;
                }
            }

            if (hasBehavior && itemBehavior._stacks != itemCounts)
            {
                itemBehavior._stacks = itemCounts;
                itemBehavior.OnStacksChanged();
            }
        }

        sealed class BodyBehaviorInfo
        {
            public readonly QualityItemBodyBehavior[] BehaviorComponents;

            public readonly int CollectionIndex;

            public BodyBehaviorInfo(QualityItemBodyBehavior[] behaviors, int collectionIndex)
            {
                BehaviorComponents = behaviors;
                CollectionIndex = collectionIndex;
            }
        }

        readonly struct QualityGroupBehaviorInfo
        {
            public readonly ItemQualityGroupIndex ItemGroupIndex;

            public readonly Type BehaviorType;

            public QualityGroupBehaviorInfo(ItemQualityGroupIndex groupIndex, Type qualityItemBehaviorType)
            {
                ItemGroupIndex = groupIndex;
                BehaviorType = qualityItemBehaviorType;
            }
        }

        readonly struct QualityGroupBehaviorCollection
        {
            public readonly QualityGroupBehaviorInfo[] Behaviors;

            public readonly FixedSizeArrayPool<QualityItemBodyBehavior> BehaviorsArrayPool;

            public QualityGroupBehaviorCollection(QualityGroupBehaviorInfo[] itemGroups, FixedSizeArrayPool<QualityItemBodyBehavior> behaviorsArrayPool)
            {
                Behaviors = itemGroups;
                BehaviorsArrayPool = behaviorsArrayPool;
            }
        }

        [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
        public sealed class ItemGroupAssociationAttribute : SearchableAttribute
        {
            public new MethodInfo target => base.target as MethodInfo;

            public QualityItemBehaviorUsageFlags Usage { get; }

            public ItemGroupAssociationAttribute(QualityItemBehaviorUsageFlags usage)
            {
                Usage = usage;
            }
        }

        [Flags]
        public enum QualityItemBehaviorUsageFlags : uint
        {
            None = 0,
            Server = 1 << 0,
            Client = 1 << 1,
            Authority = 1 << 2,
            All = Server | Client | Authority,
        }
    }
}
