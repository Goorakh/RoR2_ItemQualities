using ItemQualities.Items;
using RoR2;
using RoR2.Projectile;
using System;
using UnityEngine;

namespace ItemQualities
{
    public sealed class ExplosionRangeIndicatorScaler : MonoBehaviour
    {
        public ExplosionInfoIndex ExplosionInfoIndex = ExplosionInfoIndex.None;

        public Transform[] IndicatorTransforms = Array.Empty<Transform>();

        private ProjectileGhostController _projectileGhostController;
        private ProjectileController _projectileController;
        private GenericOwnership _genericOwnership;
        private LocalEffectOwnership _localEffectOwnership;

        private ProjectileExplosion _projectileExplosion;

        private CharacterBody _ownerBody;

        private float _lastIndicatorScaleMultiplier = 1f;

        private void Awake()
        {
            _projectileGhostController = GetComponent<ProjectileGhostController>();
            _projectileController = GetComponent<ProjectileController>();
            _projectileExplosion = GetComponent<ProjectileExplosion>();
            _genericOwnership = GetComponent<GenericOwnership>();
            _localEffectOwnership = GetComponent<LocalEffectOwnership>();
        }

        private void OnEnable()
        {
            if (_projectileGhostController)
            {
                ProjectileController ownerProjectileController = null;
                foreach (ProjectileController projectileController in InstanceTracker.GetInstancesList<ProjectileController>())
                {
                    if (projectileController && projectileController.ghost == _projectileGhostController)
                    {
                        ownerProjectileController = projectileController;
                        break;
                    }
                }

                if (ownerProjectileController)
                {
                    setProjectileControllerReference(ownerProjectileController);
                }
                else
                {
                    ProjectileHooks.OnProjectileLinkedToGhostGlobal += onProjectileLinkedToGhostGlobal;
                }
            }
            else if (_projectileController)
            {
                _projectileController.onInitialized += setProjectileControllerOwner;
                setProjectileControllerOwner(_projectileController);
            }
            else if (_genericOwnership)
            {
                _genericOwnership.onOwnerChanged += setOwnerObject;
                setOwnerObject(_genericOwnership.ownerObject);
            }
            else if (_localEffectOwnership)
            {
                _localEffectOwnership.OnOwnerChanged += setOwnerObject;
                setOwnerObject(_localEffectOwnership.OwnerObject);
            }
        }

        private void OnDisable()
        {
            bool unsetProjectileControllerReference = false;

            if (_projectileGhostController)
            {
                ProjectileHooks.OnProjectileLinkedToGhostGlobal -= onProjectileLinkedToGhostGlobal;
                unsetProjectileControllerReference = true;
            }
            else if (_projectileController)
            {
                _projectileController.onInitialized -= setProjectileControllerOwner;
            }
            else if (_genericOwnership)
            {
                _genericOwnership.onOwnerChanged -= setOwnerObject;
            }
            else if (_localEffectOwnership)
            {
                _localEffectOwnership.OnOwnerChanged -= setOwnerObject;
            }

            setOwner(null);

            if (unsetProjectileControllerReference)
            {
                setProjectileControllerReference(null);
            }
        }

        private void onProjectileLinkedToGhostGlobal(ProjectileController projectileController)
        {
            if (projectileController.ghost == _projectileGhostController)
            {
                setProjectileControllerReference(projectileController);
            }
            else if (projectileController == _projectileController)
            {
                setOwner(null);
                setProjectileControllerReference(null);
            }
        }

        private void setProjectileControllerReference(ProjectileController projectileController)
        {
            if (_projectileController == projectileController)
                return;

            if (_projectileController)
            {
                _projectileController.onInitialized -= setProjectileControllerOwner;
            }

            _projectileController = projectileController;
            _projectileExplosion = _projectileController ? _projectileController.GetComponent<ProjectileExplosion>() : null;

            if (_projectileController)
            {
                _projectileController.onInitialized += setProjectileControllerOwner;
            }

            setProjectileControllerOwner(_projectileController);
        }

        private void setProjectileControllerOwner(ProjectileController projectileController)
        {
            setOwnerObject(projectileController ? projectileController.owner : null);
        }

        private void setOwnerObject(GameObject ownerObj)
        {
            setOwner(ownerObj ? ownerObj.GetComponent<CharacterBody>() : null);
        }

        private void setOwner(CharacterBody owner)
        {
            if (_ownerBody == owner)
                return;

            if (_ownerBody)
            {
                _ownerBody.onInventoryChanged -= onOwnerInventoryChanged;
            }

            _ownerBody = owner;

            if (_ownerBody)
            {
                _ownerBody.onInventoryChanged += onOwnerInventoryChanged;
            }

            recalculateIndicatorsScale();
        }

        private void onOwnerInventoryChanged()
        {
            recalculateIndicatorsScale();
        }

        private void recalculateIndicatorsScale()
        {
            float baseRadius;
            if (ExplosionInfoIndex != ExplosionInfoIndex.None)
            {
                baseRadius = ExplosionInfoCatalog.GetExplosionInfoDef(ExplosionInfoIndex).GetDefaultRange();
            }
            else if (_projectileExplosion)
            {
                baseRadius = _projectileExplosion.blastRadius;
            }
            else
            {
                Log.Error($"No explosion info reference defined for {Util.GetGameObjectHierarchyName(gameObject)}");
                baseRadius = 0f;
            }

            if (baseRadius <= 0f)
            {
                Log.Warning($"Invalid base scale ({baseRadius}) for {Util.GetGameObjectHierarchyName(gameObject)}, aborting indicator scale adjustment");
                return;
            }

            float scaledRadius = ExplodeOnDeath.GetExplosionRadius(baseRadius, _ownerBody);

            float desiredIndicatorScaleMultiplier = scaledRadius / baseRadius;

            float indicatorsScaleMultiplier = desiredIndicatorScaleMultiplier / _lastIndicatorScaleMultiplier;
            if (!float.IsFinite(indicatorsScaleMultiplier))
            {
                Log.Error($"Infinity or NaN when calculating indicator scale for {Util.GetGameObjectHierarchyName(gameObject)}");
            }
            else
            {
                if (Mathf.Abs(indicatorsScaleMultiplier - 1f) >= Mathf.Epsilon)
                {
                    Log.Debug($"Applying indicator scale {scaledRadius} (base={baseRadius}, delta=x{indicatorsScaleMultiplier}) to {Util.GetGameObjectHierarchyName(gameObject)}");

                    foreach (Transform indicatorTransform in IndicatorTransforms)
                    {
                        if (indicatorTransform)
                        {
                            if (indicatorTransform.TryGetComponent(out ObjectScaleCurve objectScaleCurve))
                            {
                                objectScaleCurve.baseScale *= indicatorsScaleMultiplier;
                            }

                            indicatorTransform.localScale *= indicatorsScaleMultiplier;
                        }
                    }
                }

                _lastIndicatorScaleMultiplier = desiredIndicatorScaleMultiplier;
            }
        }
    }
}
