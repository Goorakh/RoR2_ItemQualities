using EntityStates.GoldGat;
using HG;
using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using RoR2BepInExPack.GameAssetPathsBetter;
using RoR2BepInExPack.Utilities;
using System;
using System.Linq;
using UnityEngine;

namespace ItemQualities.Equipments
{
    internal static class GoldGat
    {
        private static readonly BullseyeSearch _ricochetSearch = new BullseyeSearch
        {
            filterByDistinctEntity = true,
            filterByLoS = true,
            maxAngleFilter = 45f,
            sortMode = BullseyeSearch.SortMode.Angle,
            queryTriggerInteraction = QueryTriggerInteraction.Ignore,
        };

        [SystemInitializer]
        private static void Init()
        {
            AddressableUtil.LoadAssetAsync<GameObject>(RoR2_Base_GoldGat.GoldGatController_prefab).OnSuccess(goldGatAttachmentPrefab =>
            {
                goldGatAttachmentPrefab.EnsureComponent<QualityTierContext>();
            });

            On.RoR2.EquipmentSlot.UpdateGoldGat += EquipmentSlot_UpdateGoldGat;
            IL.EntityStates.GoldGat.GoldGatFire.FireBullet += GoldGatFire_FireBullet;
        }

        private static void EquipmentSlot_UpdateGoldGat(On.RoR2.EquipmentSlot.orig_UpdateGoldGat orig, EquipmentSlot self)
        {
            orig(self);

            if (self && self.goldgatControllerObject && self.goldgatControllerObject.TryGetComponentCached(out QualityTierContext qualityContext))
            {
                qualityContext.QualityTier = self.GetActiveEquipmentQualityTier();
            }
        }

        private static void GoldGatFire_FireBullet(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            fireBulletPatch(c.Goto(0));
            void fireBulletPatch(ILCursor c)
            {
                if (!c.TryGotoNext(MoveType.Before,
                                   x => x.MatchCallOrCallvirt<BulletAttack>(nameof(BulletAttack.Fire))))
                {
                    Log.Error("Failed to find BulletAttack patch location");
                    return;
                }

                VariableDefinition ricochetManagerVar = il.AddVariable<BulletRicochetManager>();

                c.Emit(OpCodes.Dup);
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate<Func<BulletAttack, GoldGatFire, BulletRicochetManager>>(modifyBulletAttack);
                c.Emit(OpCodes.Stloc, ricochetManagerVar);

                static BulletRicochetManager modifyBulletAttack(BulletAttack bulletAttack, GoldGatFire goldGatFire)
                {
                    QualityTier qualityTier = QualityTierContext.GetQualityTier(goldGatFire.gameObject);
                    if (qualityTier == QualityTier.None)
                    {
                        return null;
                    }

                    ref int bulletCount = ref GoldGatFireQualityFields.BulletCounter(goldGatFire);
                    bulletCount++;

                    int ricochetInterval = qualityTier switch
                    {
                        QualityTier.Uncommon  => 10,
                        QualityTier.Rare      => 9,
                        QualityTier.Epic      => 8,
                        QualityTier.Legendary => 7,
                        _ => throw new NotImplementedException()
                    };

                    bool shouldRicochet = bulletCount % ricochetInterval == 0;
                    if (!shouldRicochet)
                    {
                        return null;
                    }

                    int maxRicochets = qualityTier switch
                    {
                        QualityTier.Uncommon  => 1,
                        QualityTier.Rare      => 3,
                        QualityTier.Epic      => 6,
                        QualityTier.Legendary => 10,
                        _ => throw new NotImplementedException()
                    };

                    return BulletRicochetManager.Create(bulletAttack, maxRicochets);
                }

                c.Goto(c.Next, MoveType.After);

                c.Emit(OpCodes.Ldloc, ricochetManagerVar);
                c.EmitDelegate<Action<BulletRicochetManager>>(restoreBulletAttack);
                static void restoreBulletAttack(BulletRicochetManager bulletRicochetManager)
                {
                    bulletRicochetManager?.RestoreBulletAttack();
                }

                c.Emit(OpCodes.Ldnull);
                c.Emit(OpCodes.Stloc, ricochetManagerVar);
            }

            maxFireFrequencyPatch(c.Goto(0));
            void maxFireFrequencyPatch(ILCursor c)
            {
                VariableDefinition maxFireFrequencyMultiplierVar = null;

                int patchCount = 0;
                while (c.TryGotoNext(MoveType.After,
                                     x => x.MatchLdsfld<GoldGatFire>(nameof(GoldGatFire.maxFireFrequency))))
                {
                    c.Emit(OpCodes.Ldloc, maxFireFrequencyMultiplierVar ??= il.AddVariable<float>());
                    c.Emit(OpCodes.Mul);

                    patchCount++;
                }

                if (patchCount == 0)
                {
                    Log.Error("Failed to find maxFireFrequency patch location");
                    return;
                }
                else
                {
                    Log.Debug($"Found {patchCount} maxFireFrequency patch location(s)");
                }

                c.Goto(0);

                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate<Func<GoldGatFire, float>>(getMaxFireFrequencyMultiplier);
                c.Emit(OpCodes.Stloc, maxFireFrequencyMultiplierVar);

                static float getMaxFireFrequencyMultiplier(GoldGatFire self)
                {
                    QualityTier qualityTier = QualityTierContext.GetQualityTier(self.gameObject);
                    switch (qualityTier)
                    {
                        case QualityTier.None:
                            return 1.0f;
                        case QualityTier.Uncommon:
                            return 1.2f;
                        case QualityTier.Rare:
                            return 1.4f;
                        case QualityTier.Epic:
                            return 1.8f;
                        case QualityTier.Legendary:
                            return 2.5f;
                        default:
                            Log.Warning($"Quality tier {qualityTier} is not implemented");
                            return 1.0f;
                    }
                }
            }
        }

        private sealed class GoldGatFireQualityFields
        {
            private static readonly FixedConditionalWeakTable<GoldGatFire, GoldGatFireQualityFields> _goldGatFireQualityFieldsTable = new FixedConditionalWeakTable<GoldGatFire, GoldGatFireQualityFields>();

            private int _bulletCounter;
            public static ref int BulletCounter(GoldGatFire goldGatFire) => ref _goldGatFireQualityFieldsTable.GetOrAddNew(goldGatFire)._bulletCounter;
        }

        private sealed class BulletRicochetManager
        {
            private readonly BulletAttack _bulletAttack;
            private int _ricochetsRemaining;

            private readonly CharacterBody _attackerBody;
            private readonly TeamMask _searchTeamMask;

            private readonly BulletAttack.HitCallback _origHitCallback;
            private readonly BulletAttack.FilterCallback _origFilterCallback;

            private HurtBox _lastHitHurtBox;

            private BulletRicochetManager(BulletAttack bulletAttack, int ricochetsRemaining)
            {
                _bulletAttack = bulletAttack;
                _ricochetsRemaining = ricochetsRemaining;
                
                _origHitCallback = _bulletAttack.hitCallback;
                _bulletAttack.hitCallback = hitCallback;

                _origFilterCallback = _bulletAttack.filterCallback;
                _bulletAttack.filterCallback = filterCallback;
                
                _attackerBody = bulletAttack.owner.GetComponent<CharacterBody>();

                _searchTeamMask = TeamMask.allButNeutral;
                if (_attackerBody)
                {
                    _searchTeamMask.RemoveTeam(_attackerBody.teamComponent.teamIndex);
                }
            }

            public static BulletRicochetManager Create(BulletAttack bulletAttack, int maxRicochets)
            {
                return new BulletRicochetManager(bulletAttack, maxRicochets);
            }

            public void RestoreBulletAttack()
            {
                _bulletAttack.hitCallback = _origHitCallback;
                _bulletAttack.filterCallback = _origFilterCallback;
            }

            private bool hitCallback(BulletAttack bulletAttack, ref BulletAttack.BulletHit hitInfo)
            {
                bool shouldContinue = _origHitCallback(bulletAttack, ref hitInfo);

                // Don't ricochet for piercing bullets
                bool stopped = !shouldContinue;
                if (stopped)
                {
                    HurtBox hitHurtBox = hitInfo.hitHurtBox;
                    _lastHitHurtBox = hitHurtBox;
                    if (_ricochetsRemaining > 0)
                    {
                        _ricochetsRemaining--;

                        Vector3 ricochetDirection = Vector3.Reflect(bulletAttack.aimVector, hitInfo.surfaceNormal);

                        _ricochetSearch.searchOrigin = hitInfo.point;
                        _ricochetSearch.searchDirection = ricochetDirection;
                        _ricochetSearch.viewer = _attackerBody;
                        _ricochetSearch.teamMaskFilter = _searchTeamMask;
                        _ricochetSearch.maxDistanceFilter = bulletAttack.maxDistance;
                        _ricochetSearch.RefreshCandidates();

                        if (hitInfo.entityObject)
                        {
                            _ricochetSearch.FilterOutGameObject(hitInfo.entityObject);
                        }

                        HurtBox ricochetTarget = _ricochetSearch.GetResults().FirstOrDefault(h => !ReferenceEquals(h, hitHurtBox));
                        if (ricochetTarget)
                        {
                            ricochetDirection = (ricochetTarget.transform.position - hitInfo.point).normalized;
                        }

                        Ray ray = new Ray(hitInfo.point, ricochetDirection);

                        bulletAttack.FireSingle(new BulletAttack.FireSingleArgs
                        {
                            ray = ray,
                            muzzleIndex = BulletAttackExplicitTracerOriginPatch.UseExplicitTracerOriginMuzzleIndex,
                        });
                    }
                }

                return shouldContinue;
            }

            private bool filterCallback(BulletAttack bulletAttack, ref BulletAttack.BulletHit hitInfo)
            {
                return _origFilterCallback(bulletAttack, ref hitInfo) && (!hitInfo.hitHurtBox || !ReferenceEquals(hitInfo.hitHurtBox, _lastHitHurtBox));
            }
        }
    }
}
