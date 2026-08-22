using HG;
using ItemQualities.Equipments;
using ItemQualities.Utilities.Extensions;
using RoR2;
using RoR2.HudOverlay;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace ItemQualities
{
    [RequireComponent(typeof(NetworkedBodyAttachment))]
    public sealed class QualityCritOnUseAttachment : MonoBehaviour, INetworkedBodyAttachmentListener
    {
        public GameObject HudOverlayPrefab;

        public string HudOverlayChildLocatorEntry;

        private OverlayController _hudOverlayController;

        public NetworkedBodyAttachment BodyAttachment { get; private set; }

        private HashSet<CharacterBodyExtraStatsTracker> _bodiesWithWeakPointsEnabledServer;

        private CharacterBody _attachedBody;

        private float _cachedWeakPointCritMultBonus;

        private void Awake()
        {
            BodyAttachment = GetComponent<NetworkedBodyAttachment>();
        }

        private void OnEnable()
        {
            InstanceTracker.Add(this);

            if (NetworkServer.active)
            {
                _bodiesWithWeakPointsEnabledServer = SetPool<CharacterBodyExtraStatsTracker>.RentCollection();

                setAttachedBody(BodyAttachment.attachedBody);
            }

            if (NetworkClient.active)
            {
                if (HudOverlayPrefab)
                {
                    _hudOverlayController = HudOverlayManager.AddGlobalOverlay(new OverlayCreationParams
                    {
                        prefab = HudOverlayPrefab,
                        childLocatorEntry = HudOverlayChildLocatorEntry,
                    });
                }
            }
        }

        private void OnDisable()
        {
            if (_hudOverlayController != null)
            {
                HudOverlayManager.RemoveGlobalOverlay(_hudOverlayController);
                _hudOverlayController = null;
            }

            setAttachedBody(null);

            if (_bodiesWithWeakPointsEnabledServer != null)
            {
                _bodiesWithWeakPointsEnabledServer = SetPool<CharacterBodyExtraStatsTracker>.ReturnCollection(_bodiesWithWeakPointsEnabledServer);
            }

            InstanceTracker.Remove(this);
        }

        private void setAttachedBody(CharacterBody attachedBody)
        {
            if (_attachedBody == attachedBody)
                return;

            bool hadBody = _attachedBody;

            if (hadBody)
            {
                _attachedBody.onRecalculateStats -= onAttachedBodyRecalculateStats;
            }

            _attachedBody = attachedBody;

            bool hasBody = _attachedBody;

            if (hasBody)
            {
                _attachedBody.onRecalculateStats += onAttachedBodyRecalculateStats;
            }

            if (hadBody != hasBody)
            {
                if (hasBody)
                {
                    CharacterBody.onBodyStartGlobal += onBodyStartGlobal;
                    CharacterBody.onBodyDestroyGlobal += onBodyDestroyGlobal;
                }
                else
                {
                    CharacterBody.onBodyStartGlobal -= onBodyStartGlobal;
                    CharacterBody.onBodyDestroyGlobal -= onBodyDestroyGlobal;
                }
            }

            refreshAttachedBodyBuffs();

            foreach (CharacterBodyExtraStatsTracker bodyExtraStats in InstanceTracker.GetInstancesList<CharacterBodyExtraStatsTracker>())
            {
                refreshWeakPointsActive(bodyExtraStats);
            }
        }

        private void onAttachedBodyRecalculateStats(CharacterBody attachedBody)
        {
            refreshAttachedBodyBuffs();
        }

        private void refreshAttachedBodyBuffs()
        {
            float weakPointCritMultiplierBonus = 0f;

            if (_attachedBody)
            {
                BuffQualityCounts fullCrit = _attachedBody.GetBuffCounts(ItemQualitiesContent.BuffQualityGroups.FullCrit);
                weakPointCritMultiplierBonus = CritOnUse.GetCritMultiplierBonus(fullCrit.HighestQuality);
            }

            setWeakPointCritMultiplierBonus(weakPointCritMultiplierBonus);
        }

        private void setWeakPointCritMultiplierBonus(float weakPointCritMultiplierBonus)
        {
            float weakPointCritMultiplierBonusDiff = weakPointCritMultiplierBonus - _cachedWeakPointCritMultBonus;
            if (Mathf.Abs(weakPointCritMultiplierBonusDiff) < Mathf.Epsilon)
                return;

            _cachedWeakPointCritMultBonus = weakPointCritMultiplierBonus;

            if (_bodiesWithWeakPointsEnabledServer != null)
            {
                foreach (CharacterBodyExtraStatsTracker bodyExtraStats in _bodiesWithWeakPointsEnabledServer)
                {
                    bodyExtraStats.WeakPointCritMultiplierBonusServer += weakPointCritMultiplierBonusDiff;
                }

                Log.Debug($"Set new crit multiplier bonus: {weakPointCritMultiplierBonus} (diff={weakPointCritMultiplierBonusDiff}) to {_bodiesWithWeakPointsEnabledServer.Count} tracked bodies");
            }
        }

        private void onBodyStartGlobal(CharacterBody body)
        {
            if (body.TryGetComponentCached(out CharacterBodyExtraStatsTracker bodyExtraStats))
            {
                refreshWeakPointsActive(bodyExtraStats);
            }
        }

        private void onBodyDestroyGlobal(CharacterBody body)
        {
            if (body.TryGetComponentCached(out CharacterBodyExtraStatsTracker bodyExtraStats))
            {
                setWeakPointActive(bodyExtraStats, false);
            }
        }

        private void refreshWeakPointsActive(CharacterBodyExtraStatsTracker bodyExtraStats)
        {
            setWeakPointActive(bodyExtraStats, shouldEnableWeakPoints(bodyExtraStats.Body));
        }

        private bool shouldEnableWeakPoints(CharacterBody body)
        {
            return body &&
                   _attachedBody &&
                   FriendlyFireManager.ShouldDirectHitProceed(body.healthComponent, _attachedBody.teamComponent.teamIndex);
        }

        private void setWeakPointActive(CharacterBodyExtraStatsTracker bodyExtraStats, bool active)
        {
            if (_bodiesWithWeakPointsEnabledServer == null || ReferenceEquals(bodyExtraStats, null))
                return;

            bool weakPointsChanged = active ? _bodiesWithWeakPointsEnabledServer.Add(bodyExtraStats) : _bodiesWithWeakPointsEnabledServer.Remove(bodyExtraStats);
            if (!weakPointsChanged)
                return;

            if (active)
            {
                bodyExtraStats.WeakPointCritMultiplierBonusServer += _cachedWeakPointCritMultBonus;
                bodyExtraStats.WeakPointsEnabledCounterServer++;

                Log.Debug($"{Util.GetBestBodyName(_attachedBody ? _attachedBody.gameObject : null)}: Enabled weak points for {Util.GetBestBodyName(bodyExtraStats.gameObject)}");
            }
            else
            {
                bodyExtraStats.WeakPointsEnabledCounterServer--;
                bodyExtraStats.WeakPointCritMultiplierBonusServer -= _cachedWeakPointCritMultBonus;

                Log.Debug($"{Util.GetBestBodyName(_attachedBody ? _attachedBody.gameObject : null)}: Disabled weak points for {Util.GetBestBodyName(bodyExtraStats.gameObject)}");
            }
        }

        void INetworkedBodyAttachmentListener.OnAttachedBodyDiscovered(NetworkedBodyAttachment networkedBodyAttachment, CharacterBody attachedBody)
        {
            if (NetworkServer.active)
            {
                setAttachedBody(attachedBody);
            }
        }

        public static QualityCritOnUseAttachment FindAttachment(CharacterBody body)
        {
            if (!body)
                return null;

            foreach (QualityCritOnUseAttachment qualityCritOnUseAttachment in InstanceTracker.GetInstancesList<QualityCritOnUseAttachment>())
            {
                if (qualityCritOnUseAttachment.BodyAttachment.attachedBody == body)
                {
                    return qualityCritOnUseAttachment;
                }
            }

            return null;
        }

        public static QualityCritOnUseAttachment EnsureAttachment(CharacterBody body)
        {
            if (!body)
                return null;

            QualityCritOnUseAttachment qualityCritOnUseAttachment = FindAttachment(body);
            if (!qualityCritOnUseAttachment)
            {
                GameObject attachmentObj = Instantiate(ItemQualitiesContent.NetworkedPrefabs.QualityCritOnUseAttachment);

                attachmentObj.GetComponent<NetworkedBodyAttachment>().AttachToGameObjectAndSpawn(body.gameObject);

                qualityCritOnUseAttachment = attachmentObj.GetComponent<QualityCritOnUseAttachment>();
            }

            return qualityCritOnUseAttachment;
        }
    }
}
