using ItemQualities.Utilities.Extensions;
using RoR2;
using System;

namespace ItemQualities
{
    internal sealed class TeamObjectFilter : IObjectCollideFilter, IDisposable
    {
        readonly TeamIndex _teamIndex = TeamIndex.None;

        public bool InvertFilter { get; init; }

        public event Action<ObjectCollisionManager> OnFilterDirty;

        public TeamObjectFilter(TeamIndex teamIndex)
        {
            _teamIndex = teamIndex;

            TeamComponent.onJoinTeamGlobal += onJoinTeamGlobal;
            TeamComponent.onLeaveTeamGlobal += onLeaveTeamGlobal;
        }

        bool bodyPassesFilter(CharacterBody body)
        {
            if (!body || !body.teamComponent)
                return false;

            bool teamIndexMatch = body.teamComponent.teamIndex == _teamIndex;
            return teamIndexMatch != InvertFilter;
        }

        public bool PassesFilter(ObjectCollisionManager collisionManager)
        {
            if (collisionManager.Body && bodyPassesFilter(collisionManager.Body))
            {
                return true;
            }

            if (collisionManager.ProjectileController &&
                collisionManager.ProjectileController.owner &&
                collisionManager.ProjectileController.owner.TryGetComponent(out CharacterBody ownerBody) &&
                bodyPassesFilter(ownerBody))
            {
                return true;
            }

            return false;
        }

        void onLeaveTeamGlobal(TeamComponent teamComponent, TeamIndex teamIndex)
        {
            if (teamIndex == _teamIndex && OnFilterDirty != null && teamComponent.TryGetComponentCached(out ObjectCollisionManager collisionManager))
            {
                OnFilterDirty(collisionManager);
            }
        }

        void onJoinTeamGlobal(TeamComponent teamComponent, TeamIndex teamIndex)
        {
            if (teamIndex == _teamIndex && OnFilterDirty != null && teamComponent.TryGetComponentCached(out ObjectCollisionManager collisionManager))
            {
                OnFilterDirty(collisionManager);
            }
        }

        public void Dispose()
        {
            TeamComponent.onJoinTeamGlobal -= onJoinTeamGlobal;
            TeamComponent.onLeaveTeamGlobal -= onLeaveTeamGlobal;
        }
    }
}
