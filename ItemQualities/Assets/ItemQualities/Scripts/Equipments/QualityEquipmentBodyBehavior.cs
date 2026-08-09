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

namespace ItemQualities.Equipments
{
    public abstract class QualityEquipmentBodyBehavior : MonoBehaviour
    {
        private static readonly QualityGroupBehaviorCollection[] _behaviorCollectionsLookup = new QualityGroupBehaviorCollection[(int)QualityEquipmentBehaviorUsageFlags.All];

        private static readonly Dictionary<UnityObjectWrapperKey<CharacterBody>, BodyBehaviorInfo> _bodyQualityBehaviorInfoLookup = new Dictionary<UnityObjectWrapperKey<CharacterBody>, BodyBehaviorInfo>();

        private static CharacterBody _earlyAssignmentBody;

        public CharacterBody Body { get; private set; }
        public CharacterBodyExtraStatsTracker BodyStats { get; private set; }

        protected virtual void Awake()
        {
            Body = _earlyAssignmentBody;
            _earlyAssignmentBody = null;

            BodyStats = Body ? Body.GetComponentCached<CharacterBodyExtraStatsTracker>() : null;
        }

        [SystemInitializer(typeof(QualityCatalog))]
        private static void Init()
        {
            Span<List<QualityGroupBehaviorInfo>> qualityGroupBehaviorsByUsageLookup = new List<QualityGroupBehaviorInfo>[(int)QualityEquipmentBehaviorUsageFlags.All];
            foreach (ref List<QualityGroupBehaviorInfo> qualityGroupBehaviors in qualityGroupBehaviorsByUsageLookup)
            {
                qualityGroupBehaviors = new List<QualityGroupBehaviorInfo>();
            }

            foreach (EquipmentGroupAssociationAttribute equipmentGroupAttribute in SearchableAttribute.GetInstances<EquipmentGroupAssociationAttribute>()
                                                                                                      .OfType<EquipmentGroupAssociationAttribute>())
            {
                MethodInfo getEquipmentGroupMethod = equipmentGroupAttribute.target;
                if (getEquipmentGroupMethod == null)
                {
                    Log.Error("Null target method for equipment group attribute.");
                    continue;
                }

                QualityEquipmentBehaviorUsageFlags equipmentBehaviorUsage = equipmentGroupAttribute.Usage & QualityEquipmentBehaviorUsageFlags.All;

                Type qualityEquipmentBehaviorType = null;
                try
                {
                    qualityEquipmentBehaviorType = getEquipmentGroupMethod.DeclaringType;

                    if (equipmentBehaviorUsage == QualityEquipmentBehaviorUsageFlags.None)
                    {
                        Log.Error($"{nameof(EquipmentGroupAssociationAttribute)} method ({qualityEquipmentBehaviorType.FullName}.{getEquipmentGroupMethod.Name}) has no usage defined.");
                        continue;
                    }

                    if (!typeof(QualityEquipmentBodyBehavior).IsAssignableFrom(qualityEquipmentBehaviorType))
                    {
                        Log.Error($"{nameof(EquipmentGroupAssociationAttribute)} method ({qualityEquipmentBehaviorType.FullName}.{getEquipmentGroupMethod.Name}) must be declared in a type that inherits from {nameof(QualityEquipmentBodyBehavior)}. Found type: {qualityEquipmentBehaviorType.FullName}");
                        continue;
                    }

                    if (!getEquipmentGroupMethod.IsStatic)
                    {
                        Log.Error($"{nameof(EquipmentGroupAssociationAttribute)} method ({qualityEquipmentBehaviorType.FullName}.{getEquipmentGroupMethod.Name}) must be static");
                        continue;
                    }

                    if (getEquipmentGroupMethod.ReturnType != typeof(EquipmentQualityGroup))
                    {
                        Log.Error($"{nameof(EquipmentGroupAssociationAttribute)} method ({qualityEquipmentBehaviorType.FullName}.{getEquipmentGroupMethod.Name}) must return {nameof(EquipmentQualityGroup)}. Found return type: {getEquipmentGroupMethod.ReturnType.FullName}");
                        continue;
                    }

                    if (getEquipmentGroupMethod.GetParameters().Length != 0)
                    {
                        Log.Error($"{nameof(EquipmentGroupAssociationAttribute)} method ({qualityEquipmentBehaviorType.FullName}.{getEquipmentGroupMethod.Name}) cannot have parameters");
                        continue;
                    }

                    EquipmentQualityGroup targetEquipmentGroup = getEquipmentGroupMethod.Invoke(null, Array.Empty<object>()) as EquipmentQualityGroup;
                    if (!targetEquipmentGroup)
                    {
                        Log.Error($"{nameof(EquipmentGroupAssociationAttribute)} method ({qualityEquipmentBehaviorType.FullName}.{getEquipmentGroupMethod.Name}) returned null");
                        continue;
                    }

                    if (targetEquipmentGroup.GroupIndex == EquipmentQualityGroupIndex.Invalid)
                    {
                        Log.Error($"{nameof(EquipmentGroupAssociationAttribute)} method ({qualityEquipmentBehaviorType.FullName}.{getEquipmentGroupMethod.Name}) returned a group that is not registered in {nameof(QualityCatalog)}.");
                        continue;
                    }

                    // Place this behavior in the lists such that we can quickly find it again just by using the full flags value as an index into an array
                    for (QualityEquipmentBehaviorUsageFlags usageFlags = (QualityEquipmentBehaviorUsageFlags)1; usageFlags <= QualityEquipmentBehaviorUsageFlags.All; usageFlags++)
                    {
                        if ((usageFlags & equipmentBehaviorUsage) != 0)
                        {
                            List<QualityGroupBehaviorInfo> qualityGroupBehaviors = qualityGroupBehaviorsByUsageLookup[(int)usageFlags - 1];
                            qualityGroupBehaviors.Add(new QualityGroupBehaviorInfo(targetEquipmentGroup.GroupIndex, equipmentGroupAttribute.AllowOffhand, qualityEquipmentBehaviorType));
                        }
                    }
                }
                catch (Exception e)
                {
                    Log.Error_NoCallerPrefix($"Failed to register quality equipment behavior for {qualityEquipmentBehaviorType?.FullName ?? "[UNRESOLVED TYPE]"} ({getEquipmentGroupMethod.Name}): {e}");
                }
            }

            Dictionary<int, FixedSizeArrayPool<QualityEquipmentBodyBehavior>> behaviorArrayPoolBySizeCache = new Dictionary<int, FixedSizeArrayPool<QualityEquipmentBodyBehavior>>(qualityGroupBehaviorsByUsageLookup.Length);

            int numRegisteredBehaviors = 0;

            for (int i = 0; i < qualityGroupBehaviorsByUsageLookup.Length; i++)
            {
                QualityEquipmentBehaviorUsageFlags usageFlags = (QualityEquipmentBehaviorUsageFlags)(i + 1);

                List<QualityGroupBehaviorInfo> qualityGroupBehaviorsList = qualityGroupBehaviorsByUsageLookup[i];
                QualityGroupBehaviorInfo[] qualityGroupBehaviors = qualityGroupBehaviorsList.Count > 0 ? qualityGroupBehaviorsList.ToArray() : Array.Empty<QualityGroupBehaviorInfo>();

                FixedSizeArrayPool<QualityEquipmentBodyBehavior> behaviorsArrayPool = null;
                if (qualityGroupBehaviors.Length > 0)
                {
                    if (!behaviorArrayPoolBySizeCache.TryGetValue(qualityGroupBehaviors.Length, out behaviorsArrayPool))
                    {
                        behaviorsArrayPool = new FixedSizeArrayPool<QualityEquipmentBodyBehavior>(qualityGroupBehaviors.Length);
                        behaviorArrayPoolBySizeCache.Add(qualityGroupBehaviors.Length, behaviorsArrayPool);
                    }
                }

                _behaviorCollectionsLookup[i] = new QualityGroupBehaviorCollection(qualityGroupBehaviors, behaviorsArrayPool);
                numRegisteredBehaviors += qualityGroupBehaviors.Length;

                Log.Debug($"({usageFlags}) behaviors: [{string.Join(", ", qualityGroupBehaviors.Select(b => b.BehaviorType.Name))}]");
            }

            Log.Debug($"Collected {numRegisteredBehaviors} quality equipment behavior type(s)");

            if (numRegisteredBehaviors > 0)
            {
                CharacterBody.onBodyStartGlobal += onBodyStartGlobal;
                CharacterBody.onBodyDestroyGlobal += onBodyDestroyGlobal;
                CharacterBody.onBodyInventoryChangedGlobal += onBodyInventoryChangedGlobal;
            }
        }

        private static QualityEquipmentBehaviorUsageFlags getBehaviorFlagsForBody(CharacterBody body)
        {
            QualityEquipmentBehaviorUsageFlags usageFlags = QualityEquipmentBehaviorUsageFlags.None;

            if (NetworkServer.active)
            {
                usageFlags |= QualityEquipmentBehaviorUsageFlags.Server;
            }

            if (NetworkClient.active)
            {
                usageFlags |= QualityEquipmentBehaviorUsageFlags.Client;
            }

            if (body && body.hasEffectiveAuthority)
            {
                usageFlags |= QualityEquipmentBehaviorUsageFlags.Authority;
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
            // if body has an inventory OR is waiting on a master link
            if ((body.inventory || !body.masterObjectId.IsEmpty()) && !_bodyQualityBehaviorInfoLookup.ContainsKey(body))
            {
                int behaviorCollectionIndex = getBehaviorCollectionIndex(body);
                if (ArrayUtils.IsInBounds(_behaviorCollectionsLookup, behaviorCollectionIndex))
                {
                    ref readonly QualityGroupBehaviorCollection behaviorCollection = ref _behaviorCollectionsLookup[behaviorCollectionIndex];
                    if (behaviorCollection.BehaviorsArrayPool != null)
                    {
                        QualityEquipmentBodyBehavior[] qualityEquipmentBehaviors = behaviorCollection.BehaviorsArrayPool.Request();
                        BodyBehaviorInfo bodyBehaviorInfo = new BodyBehaviorInfo(qualityEquipmentBehaviors, behaviorCollectionIndex);

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

        private static void onBodyInventoryChangedGlobal(CharacterBody body)
        {
            if (!_bodyQualityBehaviorInfoLookup.TryGetValue(body, out BodyBehaviorInfo behaviorInfo))
            {
                    return;
            }

            refreshBodyQualityBehaviors(body, behaviorInfo);
        }

        private static void refreshBodyQualityBehaviors(CharacterBody body, BodyBehaviorInfo bodyBehaviorInfo)
        {
            if (body.inventory)
            {
                ref readonly QualityGroupBehaviorCollection behaviorCollection = ref _behaviorCollectionsLookup[bodyBehaviorInfo.CollectionIndex];

                EquipmentQualityGroupIndex currentEquipmentGroupIndex = QualityCatalog.FindEquipmentQualityGroupIndex(body.inventory.currentEquipmentIndex);
                QualityTier currentEquipmentQualityTier = body.inventory.GetActiveEquipmentQualityTier();

                for (int i = 0; i < behaviorCollection.Behaviors.Length; i++)
                {
                    ref readonly QualityGroupBehaviorInfo behaviorInfo = ref behaviorCollection.Behaviors[i];

                    bool shouldHaveEquipmentBehavior =
                        (currentEquipmentQualityTier != QualityTier.None && currentEquipmentGroupIndex == behaviorInfo.EquipmentGroupIndex) ||
                        (behaviorInfo.AllowOffhand && body.inventory.HasAnyQualityEquipment(behaviorInfo.EquipmentGroupIndex));

                    updateEquipmentBehavior(body, ref bodyBehaviorInfo.BehaviorComponents[i], behaviorInfo.BehaviorType, shouldHaveEquipmentBehavior);
                }
            }
            else
            {
                for (int i = 0; i < bodyBehaviorInfo.BehaviorComponents.Length; i++)
                {
                    ref QualityEquipmentBodyBehavior behavior = ref bodyBehaviorInfo.BehaviorComponents[i];
                    if (!ReferenceEquals(behavior, null))
                    {
                        Destroy(behavior);
                        behavior = null;
                    }
                }
            }
        }

        private static void updateEquipmentBehavior(CharacterBody body, ref QualityEquipmentBodyBehavior equipmentBehavior, Type qualityBehaviorType, bool shouldHaveBehavior)
        {
            bool hasBehavior = !ReferenceEquals(equipmentBehavior, null);

            if (hasBehavior != shouldHaveBehavior)
            {
                if (shouldHaveBehavior)
                {
                    _earlyAssignmentBody = body;
                    try
                    {
                        equipmentBehavior = (QualityEquipmentBodyBehavior)body.gameObject.AddComponent(qualityBehaviorType);
                    }
                    finally
                    {
                        _earlyAssignmentBody = null;
                    }

                    hasBehavior = true;
                }
                else
                {
                    Destroy(equipmentBehavior);
                    equipmentBehavior = null;

                    hasBehavior = false;
                }
            }
        }

        private sealed class BodyBehaviorInfo
        {
            public readonly QualityEquipmentBodyBehavior[] BehaviorComponents;

            public readonly int CollectionIndex;

            public BodyBehaviorInfo(QualityEquipmentBodyBehavior[] behaviors, int collectionIndex)
            {
                BehaviorComponents = behaviors;
                CollectionIndex = collectionIndex;
            }
        }

        private readonly struct QualityGroupBehaviorInfo
        {
            public readonly EquipmentQualityGroupIndex EquipmentGroupIndex;

            public readonly bool AllowOffhand;

            public readonly Type BehaviorType;

            public QualityGroupBehaviorInfo(EquipmentQualityGroupIndex groupIndex, bool allowOffhand, Type qualityEquipmentBehaviorType)
            {
                EquipmentGroupIndex = groupIndex;
                AllowOffhand = allowOffhand;
                BehaviorType = qualityEquipmentBehaviorType;
            }
        }

        private readonly struct QualityGroupBehaviorCollection
        {
            public readonly QualityGroupBehaviorInfo[] Behaviors;

            public readonly FixedSizeArrayPool<QualityEquipmentBodyBehavior> BehaviorsArrayPool;

            public QualityGroupBehaviorCollection(QualityGroupBehaviorInfo[] equipmentGroups, FixedSizeArrayPool<QualityEquipmentBodyBehavior> behaviorsArrayPool)
            {
                Behaviors = equipmentGroups;
                BehaviorsArrayPool = behaviorsArrayPool;
            }
        }

        [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
        protected sealed class EquipmentGroupAssociationAttribute : SearchableAttribute
        {
            public new MethodInfo target => base.target as MethodInfo;

            public QualityEquipmentBehaviorUsageFlags Usage { get; }

            public bool AllowOffhand { get; set; }

            public EquipmentGroupAssociationAttribute(QualityEquipmentBehaviorUsageFlags usage)
            {
                Usage = usage;
            }
        }

        [Flags]
        public enum QualityEquipmentBehaviorUsageFlags : uint
        {
            None = 0,
            Server = 1 << 0,
            Client = 1 << 1,
            Authority = 1 << 2,
            All = Server | Client | Authority,
        }
    }
}
