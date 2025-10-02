using ItemQualities.ContentManagement;
using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using RoR2;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Networking;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace ItemQualities
{
    [CreateAssetMenu(menuName = "ItemQualities/Buffs/BuffQualityGroup")]
    public class BuffQualityGroup : ScriptableObject, IAsyncContentLoadCallback
    {
        [HideInInspector]
        [NonSerialized]
        public BuffQualityGroupIndex GroupIndex = BuffQualityGroupIndex.Invalid;

        public AssetReferenceT<BuffDef> BaseBuffReference = new AssetReferenceT<BuffDef>(string.Empty);

        [SerializeField]
        BuffDef _uncommonBuff;

        [SerializeField]
        BuffDef _rareBuff;

        [SerializeField]
        BuffDef _epicBuff;

        [SerializeField]
        BuffDef _legendaryBuff;

        [HideInInspector]
        [NonSerialized]
        public BuffIndex BaseBuffIndex = BuffIndex.None;

        public BuffIndex UncommonBuffIndex => _uncommonBuff ? _uncommonBuff.buffIndex : BuffIndex.None;

        public BuffIndex RareBuffIndex => _rareBuff ? _rareBuff.buffIndex : BuffIndex.None;

        public BuffIndex EpicBuffIndex => _epicBuff ? _epicBuff.buffIndex : BuffIndex.None;

        public BuffIndex LegendaryBuffIndex => _legendaryBuff ? _legendaryBuff.buffIndex : BuffIndex.None;

        public BuffIndex GetBuffIndex(QualityTier qualityTier)
        {
            switch (qualityTier)
            {
                case QualityTier.None:
                    return BaseBuffIndex;
                case QualityTier.Uncommon:
                    return UncommonBuffIndex;
                case QualityTier.Rare:
                    return RareBuffIndex;
                case QualityTier.Epic:
                    return EpicBuffIndex;
                case QualityTier.Legendary:
                    return LegendaryBuffIndex;
                default:
                    throw new NotImplementedException($"Quality tier '{qualityTier}' is not implemented");
            }
        }

        public BuffQualityCounts GetBuffCounts(CharacterBody body)
        {
            if (!body)
                return default;

            int baseCount = body.GetBuffCount(BaseBuffIndex);
            int uncommonCount = body.GetBuffCount(UncommonBuffIndex);
            int rareCount = body.GetBuffCount(RareBuffIndex);
            int epicCount = body.GetBuffCount(EpicBuffIndex);
            int legendaryCount = body.GetBuffCount(LegendaryBuffIndex);

            return new BuffQualityCounts(baseCount, uncommonCount, rareCount, epicCount, legendaryCount);
        }

        public void EnsureBuffQualities(CharacterBody body, QualityTier buffQualityTier, bool includeBaseBuff = false)
        {
            if (!NetworkServer.active)
            {
                Log.Warning("Called on client");
                return;
            }

            BuffIndex desiredBuffIndex = GetBuffIndex(buffQualityTier);
            if (!includeBaseBuff && buffQualityTier == QualityTier.None)
                desiredBuffIndex = BuffIndex.None;

            for (QualityTier qualityTier = includeBaseBuff ? QualityTier.None : 0; qualityTier < QualityTier.Count; qualityTier++)
            {
                if (qualityTier != buffQualityTier)
                {
                    BuffIndex qualityBuffIndex = GetBuffIndex(qualityTier);

                    for (int i = body.GetBuffCount(qualityBuffIndex); i > 0; i--)
                    {
                        body.RemoveBuff(qualityBuffIndex);

                        if (desiredBuffIndex != BuffIndex.None)
                        {
                            body.AddBuff(desiredBuffIndex);
                        }
                    }
                }
            }
        }

        void OnValidate()
        {
            if (BaseBuffReference == null || !BaseBuffReference.RuntimeKeyIsValid())
            {
                Debug.LogError($"Invalid buff address in group '{name}'");
            }
        }

        IEnumerator IAsyncContentLoadCallback.OnContentLoad(IProgress<float> progressReceiver)
        {
            if (BaseBuffReference == null || !BaseBuffReference.RuntimeKeyIsValid())
            {
                Log.Error($"Invalid buff address in group '{name}'");
                progressReceiver.Report(1f);
                yield break;
            }

            AsyncOperationHandle<BuffDef> baseBuffLoad = AddressableUtil.LoadTempAssetAsync(BaseBuffReference);
            yield return baseBuffLoad.AsProgressCoroutine(progressReceiver);

            if (baseBuffLoad.Status != AsyncOperationStatus.Succeeded)
            {
                Log.Error($"Failed to load base buff for quality group '{name}': {baseBuffLoad.OperationException}");
                yield break;
            }

            BuffDef baseBuff = baseBuffLoad.Result;

            void populateBuffAsset(BuffDef buff, QualityTier qualityTier)
            {
                if (!buff)
                {
                    Log.Warning($"Missing variant '{qualityTier}' in buff group '{name}'");
                    return;
                }

                if (!buff.eliteDef)
                    buff.eliteDef = baseBuff.eliteDef;

                if (!buff.startSfx)
                    buff.startSfx = baseBuff.startSfx;
            }

            populateBuffAsset(_uncommonBuff, QualityTier.Uncommon);
            populateBuffAsset(_rareBuff, QualityTier.Rare);
            populateBuffAsset(_epicBuff, QualityTier.Epic);
            populateBuffAsset(_legendaryBuff, QualityTier.Legendary);
        }
    }
}
