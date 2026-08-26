using HG;
using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using RoR2;
using RoR2BepInExPack.GameAssetPathsBetter;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace ItemQualities.Items
{
    public sealed class AttackSpeedPerNearbyColliderQualityController : MonoBehaviour
    {
        [SystemInitializer]
        private static IEnumerator Init()
        {
            AsyncOperationHandle<GameObject> lanternAttachmentLoad = AddressableUtil.LoadAssetAsync<GameObject>(RoR2_DLC2_Items_AttackSpeedPerNearbyAllyOrEnemy.BolsteringLanternBonusIndicator_prefab);
            lanternAttachmentLoad.OnSuccess(lanternAttachment =>
            {
                lanternAttachment.EnsureComponent<AttackSpeedPerNearbyColliderQualityController>();
            });

            return lanternAttachmentLoad;
        }

        private NetworkedBodyAttachment _bodyAttachment;
        private AttackSpeedPerNearbyCollider _lanternCollider;

        private BuffQualityCounts _lastLanternBuffCounts;

        private float _targetDiameter = 40f;
        private float _diameterVelocity = 0f;

        private bool _settingDiameter = false;

        private void Awake()
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

        private void OnEnable()
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

        private void OnDisable()
        {
            GlobalEventManager.onCharacterDeathGlobal -= onCharacterDeathGlobal;
        }

        private void FixedUpdate()
        {
            if (NetworkServer.active)
            {
                CharacterBody body = _bodyAttachment ? _bodyAttachment.attachedBody : null;

                BuffQualityCounts lanternBuffCounts = body ? body.GetBuffCounts(ItemQualitiesContent.BuffQualityGroups.AttackSpeedPerNearbyAllyOrEnemyBuff) : BuffQualityCounts.zero;
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

        private void onCharacterDeathGlobal(DamageReport damageReport)
        {
            CharacterBody body = _bodyAttachment ? _bodyAttachment.attachedBody : null;
            if (!body || damageReport.attackerBody != body || !body.inventory)
                return;

            ItemQualityCounts attackSpeedPerNearbyAllyOrEnemy = body.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.AttackSpeedPerNearbyAllyOrEnemy);
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

        private void updateBuffCounts()
        {
            if (_lanternCollider.body)
            {
                _lanternCollider.ServerUpdateValuesFromInventory();
            }

            BuffQualityCounts lanternBuffCounts = BuffQualityCounts.zero;
            if (_bodyAttachment.attachedBody)
            {
                lanternBuffCounts = _bodyAttachment.attachedBody.GetBuffCounts(ItemQualitiesContent.BuffQualityGroups.AttackSpeedPerNearbyAllyOrEnemyBuff);
            }

            _lastLanternBuffCounts = lanternBuffCounts;
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
