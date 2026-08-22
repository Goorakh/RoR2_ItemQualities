using HG;
using RoR2;
using RoR2.Projectile;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ItemQualities
{
    public sealed class ObjectCollisionManager : MonoBehaviour
    {
        [SystemInitializer(typeof(ProjectileCatalog), typeof(BodyCatalog))]
        private static void Init()
        {
            foreach (GameObject bodyPrefab in BodyCatalog.allBodyPrefabs)
            {
                ensureComponent(bodyPrefab);
            }

            for (int i = 0; i < ProjectileCatalog.projectilePrefabCount; i++)
            {
                GameObject projectilePrefab = ProjectileCatalog.GetProjectilePrefab(i);
                if (projectilePrefab && projectilePrefab.GetComponentInChildren<Collider>(true))
                {
                    ensureComponent(projectilePrefab);
                }
            }

            static void ensureComponent(GameObject prefab)
            {
                if (!prefab.TryGetComponent(out ObjectCollisionManager objectCollisionManager))
                {
                    objectCollisionManager = prefab.AddComponent<ObjectCollisionManager>();
                    objectCollisionManager._ourColliders = prefab.GetComponentsInChildren<Collider>(true);
                }
            }
        }

        public CharacterBody Body { get; private set; }

        public ProjectileController ProjectileController { get; private set; }

        public CharacterBody OwnerBody { get; private set; }

        [SerializeField]
        private Collider[] _ourColliders = Array.Empty<Collider>();

        private HashSet<Collider> _ignoringCollisionsWith;

        private void Awake()
        {
            Body = GetComponent<CharacterBody>();
            ProjectileController = GetComponent<ProjectileController>();
            if (ProjectileController)
            {
                ProjectileController.onInitialized += onInitialized;
            }

            _ignoringCollisionsWith = SetPool<Collider>.RentCollection();

            ComponentCache.Add(gameObject, this);
        }

        private void OnDestroy()
        {
            _ignoringCollisionsWith = SetPool<Collider>.ReturnCollection(_ignoringCollisionsWith);

            if (ProjectileController)
            {
                ProjectileController.onInitialized -= onInitialized;
            }

            ComponentCache.Remove(gameObject, this);
        }

        private void OnEnable()
        {
            InstanceTracker.Add(this);

            IgnoredCollisionsProvider.RefreshObjectCollisions(this);
        }

        private void OnDisable()
        {
            InstanceTracker.Remove(this);

            SetIgnoredColliders(Array.Empty<Collider>());
        }

        private void onInitialized(ProjectileController projectileController)
        {
            OwnerBody = projectileController.owner ? projectileController.owner.GetComponent<CharacterBody>() : null;

            IgnoredCollisionsProvider.RefreshObjectCollisions(this);
        }

        public void SetIgnoredColliders(IReadOnlyCollection<Collider> newIgnoredColliders)
        {
            if (_ignoringCollisionsWith == null)
            {
                Log.Error("Cannot set ignored colliders on destroyed object");
                return;
            }

            if ((newIgnoredColliders == null || newIgnoredColliders.Count == 0) && _ignoringCollisionsWith.Count == 0)
                return;

            _ignoringCollisionsWith.RemoveWhere(c => !c);

            using var _ = ListPool<Collider>.RentCollection(out List<Collider> previousIgnoredColliders);

            ListUtils.EnsureCapacity(previousIgnoredColliders, _ignoringCollisionsWith.Count);
            foreach (Collider collider in _ignoringCollisionsWith)
            {
                previousIgnoredColliders.Add(collider);
            }

            if (newIgnoredColliders != null)
            {
                foreach (Collider collider in newIgnoredColliders)
                {
                    if (!collider)
                        continue;

                    if (_ignoringCollisionsWith.Add(collider))
                    {
                        setIgnoringCollisionsWith(collider, true);
                    }
                    else
                    {
                        previousIgnoredColliders.Remove(collider);
                    }
                }
            }

            foreach (Collider collider in previousIgnoredColliders)
            {
                if (_ignoringCollisionsWith.Remove(collider))
                {
                    setIgnoringCollisionsWith(collider, false);
                }
            }
        }

        private void setIgnoringCollisionsWith(Collider otherCollider, bool ignore)
        {
            if (!otherCollider)
                return;

            foreach (Collider collider in _ourColliders)
            {
                if (collider)
                {
                    Physics.IgnoreCollision(collider, otherCollider, ignore);
                }
            }

            Log.Debug($"{Util.GetGameObjectHierarchyName(gameObject)} ignoring collisions with {Util.GetGameObjectHierarchyName(otherCollider.gameObject)}: {ignore}");
        }

        public bool IgnoresCollisionsWith(Collider collider)
        {
            return _ignoringCollisionsWith != null && collider && _ignoringCollisionsWith.Contains(collider);
        }
    }
}
