using ItemQualities.Items;
using ItemQualities.Utilities.Extensions;
using RoR2;
using System;
using UnityEngine;
using UnityEngine.Networking;

namespace ItemQualities
{
    [RequireComponent(typeof(NetworkedBodyAttachment))]
    public sealed class DroneShootableAttachmentController : NetworkBehaviour, IOnKilledServerReceiver, IOnTakeDamageServerReceiver, INetworkedBodyAttachmentListener
    {
        NetworkedBodyAttachment _bodyAttachment;

        CharacterMaster _cachedOwnerMaster;
        CharacterBody _cachedOwnerBody;

        public GameObject HitEffectPrefab;

        public Gradient DamageColorGradient;

        public Renderer[] IndicatorRenderers = Array.Empty<Renderer>();

        public Transform RangeIndicator;

        public GameObject FxRoot;

        public GameObject ExplosionEffect;

        public HurtBoxGroup HurtBoxGroup;

        public IgnoredCollisionsProvider IgnoredCollisionsProvider;

        [SyncVar(hook = nameof(hookSetStoredDamage))]
        float _storedDamage;

        float _maxStoredDamage = 1f;

        float _storedDamageMultiplier = 1f;

        float _minRadius;
        float _maxRadius;

        Run.FixedTimeStamp _lastDamageTimeStamp = Run.FixedTimeStamp.positiveInfinity;
        Run.FixedTimeStamp _maxStoredDamageReachedTimeStamp = Run.FixedTimeStamp.positiveInfinity;

        Run.FixedTimeStamp _lastExplosionTimeStamp = Run.FixedTimeStamp.negativeInfinity;

        bool _limitsDirty = false;

        bool _wasHurtBoxesDisabled;

        MaterialPropertyBlock _propertyBlock;

        void Awake()
        {
            _propertyBlock = new MaterialPropertyBlock();

            _bodyAttachment = GetComponent<NetworkedBodyAttachment>();
            recalculateLimits();
        }

        void OnDestroy()
        {
            if (_bodyAttachment.attachedBody && _bodyAttachment.attachedBody.master)
            {
                MinionOwnership minionOwnership = _bodyAttachment.attachedBody.master.minionOwnership;
                minionOwnership.onOwnerDiscovered -= setOwnerMaster;
                minionOwnership.onOwnerLost -= setOwnerMaster;
            }

            setOwnerMaster(null);
        }

        void FixedUpdate()
        {
            if (_limitsDirty)
            {
                _limitsDirty = false;
                recalculateLimits();
            }

            bool disableHurtBoxes = shouldDisableHurtBoxes();
            if (_wasHurtBoxesDisabled != disableHurtBoxes)
            {
                if (disableHurtBoxes)
                {
                    HurtBoxGroup.hurtBoxesDeactivatorCounter++;
                }
                else
                {
                    HurtBoxGroup.hurtBoxesDeactivatorCounter--;
                }

                _wasHurtBoxesDisabled = disableHurtBoxes;
            }

            if (NetworkServer.active)
            {
                if (shouldDetonate())
                {
                    detonate();
                }
            }
        }

        bool shouldDetonate()
        {
            if (_storedDamage > 0)
            {
                if (_lastDamageTimeStamp.timeSince >= 2.5f)
                    return true;

                if (_storedDamage >= _maxStoredDamage && _maxStoredDamageReachedTimeStamp.timeSince >= 0.75f)
                    return true;
            }

            return false;
        }

        bool shouldDisableHurtBoxes()
        {
            if (_lastExplosionTimeStamp.timeSince <= 10f)
                return true;

            CharacterBody attachedBody = _bodyAttachment.attachedBody;
            if (!attachedBody)
                return true;

            if (attachedBody.hurtBoxGroup && attachedBody.hurtBoxGroup.hurtBoxesDeactivatorCounter > 0)
                return true;

            if (attachedBody.hasUntargetableBuff)
                return true;

            if (attachedBody.HasBuff(RoR2Content.Buffs.Immune) || attachedBody.HasBuff(RoR2Content.Buffs.HiddenInvincibility))
                return true;

            if (attachedBody.HasBuff(DLC2Content.Buffs.HiddenRejectAllDamage))
                return true;

            return false;
        }

        [Server]
        void detonate()
        {
            if (_storedDamage > 0f)
            {
                float damageFraction = Mathf.Clamp01(_storedDamage / _maxStoredDamage);
                float radius = Mathf.Lerp(_minRadius, _maxRadius, damageFraction);

                CharacterBody attackerBody = _cachedOwnerMaster ? _cachedOwnerMaster.GetBody() : null;

                BlastAttack blastAttack = new BlastAttack
                {
                    attacker = attackerBody ? attackerBody.gameObject : null,
                    inflictor = gameObject,
                    position = _bodyAttachment.attachedBody ? _bodyAttachment.attachedBody.corePosition : transform.position,
                    radius = radius,
                    baseDamage = _storedDamage,
                    crit = attackerBody && attackerBody.RollCrit(),
                    damageColorIndex = DamageColorIndex.Electrocution,
                    falloffModel = BlastAttack.FalloffModel.None,
                    procCoefficient = 1f,
                    teamIndex = _cachedOwnerMaster ? _cachedOwnerMaster.teamIndex : TeamIndex.None,
                    attackerFiltering = AttackerFiltering.NeverHitSelf,
                };

                blastAttack.Fire();

                if (ExplosionEffect)
                {
                    EffectData effectData = new EffectData
                    {
                        origin = blastAttack.position,
                        scale = blastAttack.radius
                    };

                    EffectManager.SpawnEffect(ExplosionEffect, effectData, true);
                }

                _lastExplosionTimeStamp = Run.FixedTimeStamp.now;
            }

            _storedDamage = 0f;
            _lastDamageTimeStamp = Run.FixedTimeStamp.positiveInfinity;
            _maxStoredDamageReachedTimeStamp = Run.FixedTimeStamp.positiveInfinity;
        }

        void IOnTakeDamageServerReceiver.OnTakeDamageServer(DamageReport damageReport)
        {
            if (!_bodyAttachment.attachedBody)
                return;

            if (damageReport.damageInfo.damageType.IsDamageSourceSkillBased &&
                damageReport.attackerTeamIndex == _bodyAttachment.attachedBody.teamComponent.teamIndex)
            {
                float damageToStore = Mathf.Min(_maxStoredDamage - _storedDamage, damageReport.damageDealt * _storedDamageMultiplier);
                if (damageToStore > 0f)
                {
                    _storedDamage += damageToStore;

                    float damageFraction = Mathf.Clamp01(_storedDamage / _maxStoredDamage);
                    float radius = Mathf.Lerp(_minRadius, _maxRadius, damageFraction);

                    if (HitEffectPrefab)
                    {
                        EffectManager.SpawnEffect(HitEffectPrefab, new EffectData
                        {
                            origin = _bodyAttachment.attachedBody.corePosition,
                            color = DamageColorGradient.Evaluate(damageFraction),
                            scale = radius
                        }, true);
                    }

                    if (damageFraction >= 1f)
                    {
                        _maxStoredDamageReachedTimeStamp = Run.FixedTimeStamp.now;
                    }
                }

                _lastDamageTimeStamp = Run.FixedTimeStamp.now;
            }
        }

        void IOnKilledServerReceiver.OnKilledServer(DamageReport damageReport)
        {
            if (_storedDamage > 0f)
            {
                detonate();
            }

            Destroy(gameObject);
        }

        void recalculateLimits()
        {
            float minRadius = 0f;
            float maxRadius = 15f;

            float damageStat = Run.instance.teamlessDamageCoefficient;
            float maxDamageCoefficient = 4f;

            float storedDamageMultiplier = 0.5f;

            if (_cachedOwnerMaster)
            {
                CharacterBody ownerBody = _cachedOwnerMaster.GetBody();
                if (ownerBody)
                {
                    damageStat = ownerBody.damage;
                }

                ItemQualityCounts dronesDropDynamite = default;

                if (_cachedOwnerMaster.inventory)
                {
                    dronesDropDynamite = _cachedOwnerMaster.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.DronesDropDynamite);
                }

                switch (dronesDropDynamite.HighestQuality)
                {
                    case QualityTier.Uncommon:
                        storedDamageMultiplier = 0.5f;
                        break;
                    case QualityTier.Rare:
                        storedDamageMultiplier = 1f;
                        break;
                    case QualityTier.Epic:
                        storedDamageMultiplier = 1.25f;
                        break;
                    case QualityTier.Legendary:
                        storedDamageMultiplier = 1.5f;
                        break;
                }

                maxDamageCoefficient += (1f * dronesDropDynamite.UncommonCount) +
                                        (2f * dronesDropDynamite.RareCount) +
                                        (4f * dronesDropDynamite.EpicCount) +
                                        (6f * dronesDropDynamite.LegendaryCount);
                
                maxRadius += (5f * dronesDropDynamite.UncommonCount) +
                             (10f * dronesDropDynamite.RareCount) +
                             (15f * dronesDropDynamite.EpicCount) +
                             (20f * dronesDropDynamite.LegendaryCount);

                if (ownerBody)
                {
                    maxRadius = ExplodeOnDeath.GetExplosionRadius(maxRadius, ownerBody);
                }
            }

            _maxStoredDamage = damageStat * maxDamageCoefficient;

            _storedDamageMultiplier = storedDamageMultiplier;

            _minRadius = minRadius;
            _maxRadius = maxRadius;

            refreshIndicator();
        }

        void refreshIndicator()
        {
            float damageFraction = Mathf.Clamp01(_storedDamage / _maxStoredDamage);
            float radius = Mathf.Lerp(_minRadius, _maxRadius, damageFraction);

            if (RangeIndicator)
            {
                float diameter = radius * 2f;
                RangeIndicator.localScale = new Vector3(diameter, diameter, diameter);
            }

            if (FxRoot)
            {
                FxRoot.SetActive(damageFraction > 0f);
            }

            if (IndicatorRenderers.Length > 0)
            {
                Color color = DamageColorGradient.Evaluate(damageFraction);
                foreach (Renderer renderer in IndicatorRenderers)
                {
                    if (renderer)
                    {
                        renderer.GetPropertyBlock(_propertyBlock);
                        _propertyBlock.SetColor(ShaderProperties._Color, color);
                        _propertyBlock.SetColor(ShaderProperties._TintColor, color);
                        renderer.SetPropertyBlock(_propertyBlock);
                    }
                }
            }
        }

        void setOwnerMaster(CharacterMaster ownerMaster)
        {
            if (_cachedOwnerMaster == ownerMaster)
                return;

            if (_cachedOwnerMaster)
            {
                _cachedOwnerMaster.onBodyStart -= setOwnerBody;
                _cachedOwnerMaster.onBodyDestroyed -= setOwnerBody;
            }

            _cachedOwnerMaster = ownerMaster;

            if (_cachedOwnerMaster)
            {
                _cachedOwnerMaster.onBodyStart += setOwnerBody;
                _cachedOwnerMaster.onBodyDestroyed += setOwnerBody;
            }

            setOwnerBody(_cachedOwnerMaster ? _cachedOwnerMaster.GetBody() : null);

            _limitsDirty = true;
        }

        void setOwnerBody(CharacterBody ownerBody)
        {
            if (_cachedOwnerBody == ownerBody)
                return;

            if (_cachedOwnerBody)
            {
                _cachedOwnerBody.onRecalculateStats -= onOwnerBodyRecalculateStats;
            }

            _cachedOwnerBody = ownerBody;

            if (_cachedOwnerBody)
            {
                _cachedOwnerBody.onRecalculateStats += onOwnerBodyRecalculateStats;
            }

            _limitsDirty = true;
        }

        void onOwnerBodyRecalculateStats(CharacterBody body)
        {
            _limitsDirty = true;
        }

        void INetworkedBodyAttachmentListener.OnAttachedBodyDiscovered(NetworkedBodyAttachment networkedBodyAttachment, CharacterBody attachedBody)
        {
            if (HurtBoxGroup.mainHurtBox && HurtBoxGroup.mainHurtBox.TryGetComponent(out SphereCollider mainHurtBoxCollider))
            {
                mainHurtBoxCollider.radius = attachedBody.bestFitActualRadius + 0.25f;
            }
            else
            {
                Log.Warning("Failed to set HurtBox size");
            }

            if (IgnoredCollisionsProvider)
            {
                IgnoredCollisionsProvider.CollisionWhitelistFilter = new ShooterObjectCollideFilter(attachedBody);
            }

            CharacterMaster ownerMaster = null;
            if (attachedBody.master)
            {
                ownerMaster = attachedBody.master.minionOwnership.ownerMaster;

                attachedBody.master.minionOwnership.onOwnerDiscovered += setOwnerMaster;
                attachedBody.master.minionOwnership.onOwnerLost += setOwnerMaster;
            }

            setOwnerMaster(ownerMaster);
        }

        void hookSetStoredDamage(float storedDamage)
        {
            _storedDamage = storedDamage;
            refreshIndicator();
        }

        sealed class ShooterObjectCollideFilter : IObjectCollideFilter, IDisposable
        {
            readonly CharacterBody _attachedBody;

            public event Action<ObjectCollisionManager> OnFilterDirty;

            public ShooterObjectCollideFilter(CharacterBody attachedBody)
            {
                _attachedBody = attachedBody;

                TeamComponent.onJoinTeamGlobal += onJoinTeamGlobal;
                TeamComponent.onLeaveTeamGlobal += onLeaveTeamGlobal;

                MinionOwnership.onMinionOwnerChangedGlobal += onMinionOwnerChangedGlobal;
            }

            public void Dispose()
            {
                TeamComponent.onJoinTeamGlobal -= onJoinTeamGlobal;
                TeamComponent.onLeaveTeamGlobal -= onLeaveTeamGlobal;

                MinionOwnership.onMinionOwnerChangedGlobal -= onMinionOwnerChangedGlobal;

                OnFilterDirty = null;
            }

            void onJoinTeamGlobal(TeamComponent teamComponent, TeamIndex teamIndex)
            {
                if (OnFilterDirty != null &&
                    _attachedBody &&
                    teamIndex == _attachedBody.teamComponent.teamIndex &&
                    teamComponent.TryGetComponentCached(out ObjectCollisionManager collisionManager))
                {
                    OnFilterDirty(collisionManager);
                }
            }

            void onLeaveTeamGlobal(TeamComponent teamComponent, TeamIndex teamIndex)
            {
                if (OnFilterDirty != null &&
                    _attachedBody &&
                    teamIndex == _attachedBody.teamComponent.teamIndex &&
                    teamComponent.TryGetComponentCached(out ObjectCollisionManager collisionManager))
                {
                    OnFilterDirty(collisionManager);
                }
            }

            void onMinionOwnerChangedGlobal(MinionOwnership minionOwnership)
            {
                if (OnFilterDirty != null && minionOwnership.TryGetComponent(out CharacterMaster master))
                {
                    GameObject bodyObject = master.GetBodyObject();
                    if (bodyObject && bodyObject.TryGetComponentCached(out ObjectCollisionManager collisionManager))
                    {
                        OnFilterDirty(collisionManager);
                    }
                }
            }

            bool bodyPassesFilter(CharacterBody body)
            {
                return body &&
                       body != _attachedBody &&
                       (body.teamComponent.teamIndex == _attachedBody.teamComponent.teamIndex ||
                        (body.master && _attachedBody.master && _attachedBody.master.minionOwnership.ownerMaster == body.master));
            }

            public bool PassesFilter(ObjectCollisionManager collisionManager)
            {
                return collisionManager && (bodyPassesFilter(collisionManager.Body) || bodyPassesFilter(collisionManager.OwnerBody));
            }
        }
    }
}
