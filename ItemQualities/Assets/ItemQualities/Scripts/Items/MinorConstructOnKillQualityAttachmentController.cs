using HG;
using ItemQualities.Utilities.Extensions;
using RoR2;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;
using UnityEngine.Networking;

namespace ItemQualities.Items
{
    [RequireComponent(typeof(NetworkedBodyAttachment))]
    [RequireComponent(typeof(TetherVfxOrigin))]
    public sealed class MinorConstructOnKillQualityAttachmentController : MonoBehaviour, INetworkedBodyAttachmentListener
    {
        [SerializeField]
        [Min(0f)]
        private float _updateInterval = 0.2f;

        private int _maxTargets = 10;

        private float _maxRadius;

        private QualityTier _attachmentQualityTier = QualityTier.None;

        private TetherVfxOrigin _tetherOrigin;

        private CharacterBody _attachedBody;

        private float _updateTimer;

        private readonly List<TargetInfo> _currentTargets = new List<TargetInfo>();

        private void Awake()
        {
            _tetherOrigin = GetComponent<TetherVfxOrigin>();
        }

        private void OnDisable()
        {
            foreach (TargetInfo targetInfo in _currentTargets)
            {
                onTargetLost(targetInfo);
            }

            _currentTargets.Clear();

            setAttachedBody(null);
        }

        private void FixedUpdate()
        {
            _updateTimer += Time.fixedDeltaTime;
            if (_updateTimer >= _updateInterval)
            {
                _updateTimer = 0f;
                updateNearbyAllies();
            }
        }

        private void updateNearbyAllies()
        {
            using var _ = ListPool<TargetInfo>.RentCollection(out List<TargetInfo> nearbyTargets);

            if (_attachedBody)
            {
                Vector3 attachedBodyPosition = _attachedBody.corePosition;
                float sqrRadius = _maxRadius * _maxRadius;

                /*ReadOnlyCollection<TeamComponent> allTeamMembers = TeamComponent.GetTeamMembers(_attachedBody.teamComponent.teamIndex);
                int maxTargets = Mathf.Min(_maxTargets, allTeamMembers.Count);

                int nearestTeamMembersCount = 0;
                TeamMember[] nearestTeamMembers = new TeamMember[maxTargets];

                foreach (TeamComponent teamComponent in allTeamMembers)
                {
                    if (ReferenceEquals(teamComponent.body, _attachedBody))
                        continue;

                    if (!teamComponent.body || !teamComponent.body.healthComponent || !teamComponent.body.healthComponent.alive)
                        continue;

                    if (teamComponent.body.hurtBoxGroup && teamComponent.body.hurtBoxGroup.hurtBoxesDeactivatorCounter > 0)
                        continue;

                    if (teamComponent.body.GetVisibilityLevel(_attachedBody) < VisibilityLevel.Revealed)
                        continue;

                    float teamMemberDistanceSqr = (teamComponent.body.corePosition - attachedBodyPosition).sqrMagnitude;
                    if (teamMemberDistanceSqr <= sqrRadius)
                    {
                        TeamMember teamMember = new TeamMember(teamComponent.body, teamMemberDistanceSqr);
                        int orderedTeamMemberIndex = Array.BinarySearch(nearestTeamMembers, 0, nearestTeamMembersCount, teamMember, TeamMember.DistanceComparer.Instance);

                        if (orderedTeamMemberIndex < 0)
                        {
                            orderedTeamMemberIndex = ~orderedTeamMemberIndex;
                        }

                        if (nearestTeamMembersCount < maxTargets)
                        {
                            ArrayUtils.ArrayInsertNoResize(nearestTeamMembers, ++nearestTeamMembersCount, orderedTeamMemberIndex, teamMember);
                        }
                        else if (orderedTeamMemberIndex < maxTargets)
                        {
                            nearestTeamMembers[orderedTeamMemberIndex] = teamMember;
                        }
                    }
                }

                ListUtils.EnsureCapacity(nearbyBodies, nearestTeamMembersCount);
                for (int i = 0; i < nearestTeamMembersCount; i++)
                {
                    nearbyBodies.Add(nearestTeamMembers[i].Body);
                }*/

                ReadOnlyCollection<TeamComponent> allTeamMembers = TeamComponent.GetTeamMembers(_attachedBody.teamComponent.teamIndex);
                int maxTargets = Mathf.Min(_maxTargets, allTeamMembers.Count);
                ListUtils.EnsureCapacity(nearbyTargets, maxTargets);

                foreach (TeamComponent teamComponent in allTeamMembers)
                {
                    if (ReferenceEquals(teamComponent.body, _attachedBody))
                        continue;

                    if (!teamComponent.body || !teamComponent.body.healthComponent || !teamComponent.body.healthComponent.alive)
                        continue;

                    if (teamComponent.body.hurtBoxGroup && teamComponent.body.hurtBoxGroup.hurtBoxesDeactivatorCounter > 0)
                        continue;

                    if (teamComponent.body.GetVisibilityLevel(_attachedBody) < VisibilityLevel.Revealed)
                        continue;

                    float teamMemberDistanceSqr = (teamComponent.body.corePosition - attachedBodyPosition).sqrMagnitude;
                    if (teamMemberDistanceSqr <= sqrRadius)
                    {
                        nearbyTargets.Add(new TargetInfo(teamComponent.body));

                        if (nearbyTargets.Count >= maxTargets)
                        {
                            break;
                        }
                    }
                }
            }

            if (_currentTargets.Count > 0 || nearbyTargets.Count > 0)
            {
                bool targetsChanged = false;

                foreach (TargetInfo targetInfo in nearbyTargets)
                {
                    if (!_currentTargets.Contains(targetInfo))
                    {
                        onTargetFound(targetInfo);
                        targetsChanged = true;
                    }
                }

                foreach (TargetInfo targetInfo in _currentTargets)
                {
                    if (!nearbyTargets.Contains(targetInfo))
                    {
                        onTargetLost(targetInfo);
                        targetsChanged = true;
                    }
                }

                if (targetsChanged)
                {
                    ListUtils.CloneTo(nearbyTargets, _currentTargets);

                    updateTetherTargets();
                }
            }
        }

        private void onTargetFound(TargetInfo targetInfo)
        {
            if (NetworkServer.active && targetInfo.Master)
            {
                targetInfo.Master.inventory.GiveItemChanneled(ItemQualitiesContent.ItemQualityGroups.ConstructBubble.GetItemIndex(_attachmentQualityTier));
            }
        }

        private void onTargetLost(TargetInfo targetInfo)
        {
            if (NetworkServer.active && targetInfo.Master)
            {
                targetInfo.Master.inventory.RemoveItemChanneled(ItemQualitiesContent.ItemQualityGroups.ConstructBubble.GetItemIndex(_attachmentQualityTier));
            }
        }

        private void updateTetherTargets()
        {
            using var _ = ListPool<Transform>.RentCollection(out List<Transform> tetheredTransforms);
            ListUtils.EnsureCapacity(tetheredTransforms, _currentTargets.Count);

            foreach (TargetInfo targetInfo in _currentTargets)
            {
                CharacterBody body = targetInfo.Body;
                if (body)
                {
                    tetheredTransforms.Add(body.coreTransform ? body.coreTransform : ((MonoBehaviour)body).transform);
                }
            }

            _tetherOrigin.SetTetheredTransforms(tetheredTransforms);
        }

        private void setAttachedBody(CharacterBody newAttachedBody)
        {
            if (ReferenceEquals(_attachedBody, newAttachedBody))
                return;

            if (!ReferenceEquals(_attachedBody, null))
            {
                _attachedBody.onInventoryChanged -= recalculateStats;
            }

            _attachedBody = newAttachedBody;

            if (!ReferenceEquals(_attachedBody, null))
            {
                _attachedBody.onInventoryChanged += recalculateStats;
            }

            recalculateStats();
        }

        private void recalculateStats()
        {
            ItemQualityCounts stacks = new ItemQualityCounts();
            if (_attachedBody && _attachedBody.inventory)
            {
                stacks = _attachedBody.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.MinorConstructOnKill);
            }

            QualityTier prevAttachmentQualityTier = _attachmentQualityTier;
            _attachmentQualityTier = stacks.HighestQuality;

            if (prevAttachmentQualityTier != _attachmentQualityTier)
            {
                if (NetworkServer.active)
                {
                    ItemIndex prevAttachmentItem = ItemQualitiesContent.ItemQualityGroups.ConstructBubble.GetItemIndex(prevAttachmentQualityTier);
                    ItemIndex newAttachmentItem = ItemQualitiesContent.ItemQualityGroups.ConstructBubble.GetItemIndex(_attachmentQualityTier);

                    foreach (TargetInfo targetInfo in _currentTargets)
                    {
                        if (targetInfo.Master)
                        {
                            if (prevAttachmentItem != ItemIndex.None)
                            {
                                targetInfo.Master.inventory.RemoveItemChanneled(prevAttachmentItem);
                            }

                            if (newAttachmentItem != ItemIndex.None)
                            {
                                targetInfo.Master.inventory.GiveItemChanneled(newAttachmentItem);
                            }
                        }
                    }
                }
            }

            _maxRadius = (stacks.UncommonCount * 30f) +
                         (stacks.RareCount * 60f) +
                         (stacks.EpicCount * 100f) +
                         (stacks.LegendaryCount * 300f);

            switch (_attachmentQualityTier)
            {
                case QualityTier.None:
                    _maxTargets = 0;
                    break;
                case QualityTier.Uncommon:
                    _maxTargets = 10;
                    break;
                case QualityTier.Rare:
                    _maxTargets = 20;
                    break;
                case QualityTier.Epic:
                    _maxTargets = 35;
                    break;
                case QualityTier.Legendary:
                    _maxTargets = int.MaxValue;
                    break;
                default:
                    _maxTargets = 0;
                    Log.Warning($"Quality tier {_attachmentQualityTier} is not implemented");
                    break;
            }
        }

        void INetworkedBodyAttachmentListener.OnAttachedBodyDiscovered(NetworkedBodyAttachment networkedBodyAttachment, CharacterBody attachedBody)
        {
            setAttachedBody(attachedBody);
        }

        private readonly struct TeamMember
        {
            public readonly CharacterBody Body;
            public readonly float SqrDistance;

            public TeamMember(CharacterBody body, float sqrDistance)
            {
                Body = body;
                SqrDistance = sqrDistance;
            }

            public sealed class DistanceComparer : IComparer<TeamMember>
            {
                public static DistanceComparer Instance { get; } = new DistanceComparer();

                public int Compare(TeamMember x, TeamMember y)
                {
                    return x.SqrDistance.CompareTo(y.SqrDistance);
                }
            }
        }

        private sealed class TargetInfo : IEquatable<TargetInfo>
        {
            public readonly CharacterMaster Master;

            CharacterBody _body;
            public CharacterBody Body
            {
                get
                {
                    if (Master && (!_body || !ReferenceEquals(_body.master, Master)))
                    {
                        _body = Master.GetBody();
                    }

                    return _body;
                }
            }

            public TargetInfo(CharacterBody body)
            {
                Master = body ? body.master : null;
                _body = body;
            }

            public override bool Equals(object obj)
            {
                return Equals(obj as TargetInfo);
            }

            public bool Equals(TargetInfo other)
            {
                if (other is null)
                    return false;

                if (ReferenceEquals(Master, null) != ReferenceEquals(other.Master, null))
                    return false;

                CharacterBody body = Body;
                CharacterBody otherBody = other.Body;
                if (ReferenceEquals(body, null) != ReferenceEquals(otherBody, null))
                    return false;

                return ReferenceEquals(Master, other.Master) || ReferenceEquals(body, otherBody);
            }

            public override int GetHashCode()
            {
                if (Master)
                {
                    return Master.GetHashCode();
                }

                if (Body)
                {
                    return Body.GetHashCode();
                }

                return 0;
            }

            public static bool operator ==(TargetInfo left, TargetInfo right)
            {
                return Equals(left, right);
            }

            public static bool operator !=(TargetInfo left, TargetInfo right)
            {
                return !Equals(left, right);
            }
        }
    }
}
