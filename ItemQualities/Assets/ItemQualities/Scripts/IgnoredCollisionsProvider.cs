using HG;
using RoR2;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ItemQualities
{
    public sealed class IgnoredCollisionsProvider : MonoBehaviour
    {
        private static readonly HashSet<ObjectCollisionManager> _dirtyCollisionManagers = new HashSet<ObjectCollisionManager>();

        public Collider[] Colliders = Array.Empty<Collider>();

        private IObjectCollideFilter _collisionWhitelistFilter;
        public IObjectCollideFilter CollisionWhitelistFilter
        {
            get
            {
                return _collisionWhitelistFilter;
            }
            set
            {
                if (_collisionWhitelistFilter == value)
                    return;

                if (_collisionWhitelistFilter != null)
                {
                    _collisionWhitelistFilter.OnFilterDirty -= markObjectCollisionManagerDirty;

                    if (_collisionWhitelistFilter is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                }

                _collisionWhitelistFilter = value;

                if (_collisionWhitelistFilter != null)
                {
                    _collisionWhitelistFilter.OnFilterDirty += markObjectCollisionManagerDirty;
                }

                if (isActiveAndEnabled)
                {
                    markAllObjectCollisionManagersDirty();
                }
            }
        }

        private void OnDestroy()
        {
            CollisionWhitelistFilter = null;
        }

        private void OnEnable()
        {
            InstanceTracker.Add(this);
            markAllObjectCollisionManagersDirty();
        }

        private void OnDisable()
        {
            InstanceTracker.Remove(this);
            markAllObjectCollisionManagersDirty();
        }

        private static void markAllObjectCollisionManagersDirty()
        {
            foreach (ObjectCollisionManager collisionManager in InstanceTracker.GetInstancesList<ObjectCollisionManager>())
            {
                markObjectCollisionManagerDirty(collisionManager);
            }
        }

        private static void markObjectCollisionManagerDirty(ObjectCollisionManager collisionManager)
        {
            if (_dirtyCollisionManagers.Add(collisionManager) && _dirtyCollisionManagers.Count == 1)
            {
                RoR2Application.onNextUpdate += refreshAllDirtyCollisionManagers;
            }
        }

        private static void refreshAllDirtyCollisionManagers()
        {
            foreach (ObjectCollisionManager collisionManager in _dirtyCollisionManagers)
            {
                if (collisionManager)
                {
                    RefreshObjectCollisions(collisionManager);
                }
            }

            _dirtyCollisionManagers.Clear();
        }

        public static void RefreshObjectCollisions(ObjectCollisionManager collisionManager)
        {
            if (!collisionManager)
                throw new ArgumentNullException(nameof(collisionManager));

            using var _ = SetPool<Collider>.RentCollection(out HashSet<Collider> ignoredColliders);

            foreach (IgnoredCollisionsProvider ignoredCollisionsProvider in InstanceTracker.GetInstancesList<IgnoredCollisionsProvider>())
            {
                if (ignoredCollisionsProvider.Colliders == null || ignoredCollisionsProvider.Colliders.Length == 0)
                    continue;

                bool shouldCollideWithBody = false;
                if (ignoredCollisionsProvider.CollisionWhitelistFilter != null)
                {
                    shouldCollideWithBody = true;
                    try
                    {
                        shouldCollideWithBody = ignoredCollisionsProvider.CollisionWhitelistFilter.PassesFilter(collisionManager);
                    }
                    catch (Exception e)
                    {
                        Log.Error_NoCallerPrefix(e.ToString());
                    }
                }

                if (!shouldCollideWithBody)
                {
                    ignoredColliders.UnionWith(ignoredCollisionsProvider.Colliders);
                }
            }

            collisionManager.SetIgnoredColliders(ignoredColliders);
        }
    }
}
