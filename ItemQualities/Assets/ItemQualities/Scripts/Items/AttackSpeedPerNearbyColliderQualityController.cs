using HG;
using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using RoR2;
using RoR2BepInExPack.GameAssetPathsBetter;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace ItemQualities.Items
{
    public sealed class AttackSpeedPerNearbyColliderQualityController : MonoBehaviour
    {
        [SystemInitializer]
        static IEnumerator Init()
        {
            AsyncOperationHandle<GameObject> lanternAttachmentLoad = AddressableUtil.LoadTempAssetAsync<GameObject>(RoR2_DLC2_Items_AttackSpeedPerNearbyAllyOrEnemy.BolsteringLanternBonusIndicator_prefab);
            lanternAttachmentLoad.OnSuccess(lanternAttachment =>
            {
                lanternAttachment.EnsureComponent<AttackSpeedPerNearbyColliderQualityController>();
            });

            return lanternAttachmentLoad;
        }

        NetworkedBodyAttachment _bodyAttachment;
        AttackSpeedPerNearbyCollider _lanternCollider;

        BuffQualityCounts _lastLanternBuffCounts;

        float _targetDiameter = 40f;
        float _diameterVelocity = 0f;

        bool _settingDiameter = false;

        void Awake()
        {
            _bodyAttachment = GetComponent<NetworkedBodyAttachment>();

            if (!_bodyAttachment)
            {
                Log.Error($"{Util.GetGameObjectHierarchyName(gameObject)} is missing NetworkedBodyAttachment component");
                enabled = false;
                return;
            }

            _lanternCollider = GetComponent<AttackSpeedPerNearbyCollider>();
            if (!_lanternCollider)
            {
                Log.Error($"{Util.GetGameObjectHierarchyName(gameObject)} is missing AttackSpeedPerNearbyCollider component");
                enabled = false;
                return;
            }
        }

        void OnEnable()
        {
            if (NetworkServer.active)
            {
                GlobalEventManager.onCharacterDeathGlobal += onCharacterDeathGlobal;

                updateBuffCounts();
            }

            if (_lanternCollider.indicatorSphere)
            {
                _targetDiameter = _lanternCollider.indicatorSphere.transform.localScale.x;
            }
        }

        void OnDisable()
        {
            GlobalEventManager.onCharacterDeathGlobal -= onCharacterDeathGlobal;
        }

        void FixedUpdate()
        {
            if (NetworkServer.active)
            {
                CharacterBody body = _bodyAttachment ? _bodyAttachment.attachedBody : null;

                BuffQualityCounts lanternBuffCounts = ItemQualitiesContent.BuffQualityGroups.AttackSpeedPerNearbyAllyOrEnemyBuff.GetBuffCounts(body);
                if (lanternBuffCounts != _lastLanternBuffCounts)
                {
                    updateBuffCounts();
                }
            }

            if (_lanternCollider.indicatorSphere)
            {
                float diameter = _lanternCollider.indicatorSphere.transform.localScale.x;
                if (diameter != _targetDiameter)
                {
                    float newDiameter = Mathf.SmoothDamp(diameter, _targetDiameter, ref _diameterVelocity, 0.3f);

                    _settingDiameter = true;
                    try
                    {
                        _lanternCollider.SetIndicatorDiameter(newDiameter);
                    }
                    finally
                    {
                        _settingDiameter = false;
                    }
                }
            }
        }

        void onCharacterDeathGlobal(DamageReport damageReport)
        {
            CharacterBody body = _bodyAttachment ? _bodyAttachment.attachedBody : null;
            if (!body || damageReport.attackerBody != body)
                return;

            ItemQualityCounts attackSpeedPerNearbyAllyOrEnemy = ItemQualitiesContent.ItemQualityGroups.AttackSpeedPerNearbyAllyOrEnemy.GetItemCounts(body.inventory);
            if (attackSpeedPerNearbyAllyOrEnemy.TotalQualityCount > 0)
            {
                QualityTier qualityTier = attackSpeedPerNearbyAllyOrEnemy.HighestQuality;

                if (damageReport.victimBody && (damageReport.victimBody.corePosition - body.corePosition).magnitude <= _lanternCollider.sphereCollider.radius)
                {
                    float buffDuration = 0f;
                    switch (qualityTier)
                    {
                        case QualityTier.Uncommon:
                            buffDuration = 3f;
                            break;
                        case QualityTier.Rare:
                            buffDuration = 5f;
                            break;
                        case QualityTier.Epic:
                            buffDuration = 7f;
                            break;
                        case QualityTier.Legendary:
                            buffDuration = 10f;
                            break;
                        default:
                            Log.Error($"Quality tier {qualityTier} is not implemented");
                            break;
                    }

                    if (buffDuration > 0f)
                    {
                        BuffIndex buffIndex = ItemQualitiesContent.BuffQualityGroups.AttackSpeedPerNearbyAllyOrEnemyBuff.GetBuffIndex(qualityTier);
                        body.AddTimedBuff(buffIndex, buffDuration);

                        updateBuffCounts();
                    }
                }
            }
        }

        void updateBuffCounts()
        {
            if (_lanternCollider.body)
            {
                _lanternCollider.ServerUpdateValuesFromInventory();
            }

            _lastLanternBuffCounts = ItemQualitiesContent.BuffQualityGroups.AttackSpeedPerNearbyAllyOrEnemyBuff.GetBuffCounts(_bodyAttachment.attachedBody);
        }

        public bool HandleSetDiameter(float diameter)
        {
            if (_settingDiameter || !enabled || !_lanternCollider.indicatorSphere)
            {
                return true;
            }
            else
            {
                _targetDiameter = diameter;
                return false;
            }
        }
    }
}
