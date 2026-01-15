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

        ProjectileController _projectileController;
        GenericOwnership _genericOwnership;

        ProjectileExplosion _projectileExplosion;

        CharacterBody _ownerBody;

        float _cachedDefaultRange = 10f;

        float _lastIndicatorScaleMultiplier = 1f;

        void Awake()
        {
            _projectileController = GetComponent<ProjectileController>();
            _projectileExplosion = GetComponent<ProjectileExplosion>();
            _genericOwnership = GetComponent<GenericOwnership>();
        }

        void OnEnable()
        {
            if (_projectileController)
            {
                _projectileController.onInitialized += setProjectileControllerOwner;
                setProjectileControllerOwner(_projectileController);
            }
            else if (_genericOwnership)
            {
                _genericOwnership.onOwnerChanged += setOwnerObject;
                setOwnerObject(_genericOwnership.ownerObject);
            }
        }

        void OnDisable()
        {
            if (_projectileController)
            {
                _projectileController.onInitialized -= setProjectileControllerOwner;
            }
            else if (_genericOwnership)
            {
                _genericOwnership.onOwnerChanged -= setOwnerObject;
            }

            setOwner(null);
        }

        void setProjectileControllerOwner(ProjectileController projectileController)
        {
            setOwner(projectileController.owner ? projectileController.owner.GetComponent<CharacterBody>() : null);
        }

        void setOwnerObject(GameObject ownerObj)
        {
            setOwner(ownerObj ? ownerObj.GetComponent<CharacterBody>() : null);
        }

        void setOwner(CharacterBody owner)
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

        void onOwnerInventoryChanged()
        {
            recalculateIndicatorsScale();
        }

        void recalculateIndicatorsScale()
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
                            indicatorTransform.localScale *= indicatorsScaleMultiplier;
                        }
                    }
                }

                _lastIndicatorScaleMultiplier = desiredIndicatorScaleMultiplier;
            }
        }
    }
}
