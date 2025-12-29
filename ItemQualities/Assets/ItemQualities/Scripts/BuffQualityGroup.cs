using ItemQualities.ContentManagement;
using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using RoR2;
using RoR2.ContentManagement;
using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Networking;
using UnityEngine.ResourceManagement.AsyncOperations;

using Path = System.IO.Path;

namespace ItemQualities
{
    [CreateAssetMenu(menuName = "ItemQualities/Buffs/BuffQualityGroup")]
    public sealed class BuffQualityGroup : ScriptableObject, IAsyncContentLoadCallback
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

            int baseCount = BaseBuffIndex != BuffIndex.None ? body.GetBuffCount(BaseBuffIndex) : 0;
            int uncommonCount = UncommonBuffIndex != BuffIndex.None ? body.GetBuffCount(UncommonBuffIndex) : 0;
            int rareCount = RareBuffIndex != BuffIndex.None ? body.GetBuffCount(RareBuffIndex) : 0;
            int epicCount = EpicBuffIndex != BuffIndex.None ? body.GetBuffCount(EpicBuffIndex) : 0;
            int legendaryCount = LegendaryBuffIndex != BuffIndex.None ? body.GetBuffCount(LegendaryBuffIndex) : 0;

            return new BuffQualityCounts(baseCount, uncommonCount, rareCount, epicCount, legendaryCount);
        }

        public bool HasBuff(CharacterBody body)
        {
            return HasBuff(body, out _);
        }

        public bool HasBuff(CharacterBody body, out QualityTier buffQualityTier)
        {
            BuffQualityCounts buffCounts = GetBuffCounts(body);
            buffQualityTier = buffCounts.HighestQuality;
            return buffCounts.TotalCount > 0;
        }

        public bool HasQualityBuff(CharacterBody body)
        {
            return HasQualityBuff(body, out _);
        }

        public bool HasQualityBuff(CharacterBody body, out QualityTier buffQualityTier)
        {
            return HasBuff(body, out buffQualityTier) && buffQualityTier > QualityTier.None;
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

            BuffDef desiredBuffDef = BuffCatalog.GetBuffDef(desiredBuffIndex);

            for (QualityTier qualityTier = includeBaseBuff ? QualityTier.None : 0; qualityTier < QualityTier.Count; qualityTier++)
            {
                if (qualityTier != buffQualityTier)
                {
                    BuffIndex qualityBuffIndex = GetBuffIndex(qualityTier);
                    if (qualityBuffIndex != BuffIndex.None)
                    {
                        for (int i = body.GetBuffCount(qualityBuffIndex); i > 0; i--)
                        {
                            body.RemoveBuff(qualityBuffIndex);

                            if (desiredBuffIndex != BuffIndex.None && desiredBuffDef && (desiredBuffDef.canStack || !body.HasBuff(desiredBuffIndex)))
                            {
                                body.AddBuff(desiredBuffIndex);
                            }
                        }
                    }
                }
            }
        }

        IEnumerator IAsyncContentLoadCallback.OnContentLoad(IProgress<float> progressReceiver)
        {
            if (BaseBuffReference == null || !BaseBuffReference.RuntimeKeyIsValid())
            {
                progressReceiver.Report(1f);
                yield break;
            }

            AsyncOperationHandle<BuffDef> baseBuffLoad = AssetAsyncReferenceManager<BuffDef>.LoadAsset(BaseBuffReference);
            yield return baseBuffLoad.AsProgressCoroutine(progressReceiver);

            if (baseBuffLoad.IsValid() && baseBuffLoad.Status == AsyncOperationStatus.Succeeded)
            {
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
            else
            {
                Log.Error($"Failed to load base buff for quality group '{name}': {(baseBuffLoad.IsValid() ? baseBuffLoad.OperationException : "Invalid handle")}");
                yield break;
            }

            AssetAsyncReferenceManager<BuffDef>.UnloadAsset(BaseBuffReference);
        }

#if UNITY_EDITOR
        [ContextMenu("Generate BuffDefs")]
        void GenerateBuffs()
        {
            string baseBuffName = name;
            if (baseBuffName.StartsWith("bg"))
                baseBuffName = baseBuffName.Substring(2);

            string currentDirectory = Path.GetDirectoryName(AssetDatabase.GetAssetPath(this));

            AsyncOperationHandle<BuffDef> baseBuffLoadHandle = default;
            BuffDef baseBuffDef = null;
            if (BaseBuffReference != null && BaseBuffReference.RuntimeKeyIsValid())
            {
                baseBuffLoadHandle = BaseBuffReference.LoadAssetAsync<BuffDef>();

                baseBuffDef = baseBuffLoadHandle.WaitForCompletion();
            }

            using ScopedAsyncOperationHandle<BuffDef> baseBuffLoadScope = new ScopedAsyncOperationHandle<BuffDef>(baseBuffLoadHandle);

            Texture2D baseIconTexture = baseBuffDef && baseBuffDef.iconSprite ? baseBuffDef.iconSprite.texture : null;

            BuffDef createBuffDef(QualityTier qualityTier)
            {
                string buffName = baseBuffName + qualityTier;

                BuffDef buffDef = ScriptableObject.CreateInstance<BuffDef>();
                buffDef.name = $"bd{buffName}";
                buffDef.buffColor = Color.white;

                if (baseBuffDef)
                {
                    buffDef.canStack = baseBuffDef.canStack;
                    buffDef.isDebuff = baseBuffDef.isDebuff;
                    buffDef.isDOT = baseBuffDef.isDOT;
                    buffDef.ignoreGrowthNectar = baseBuffDef.ignoreGrowthNectar;
                    buffDef.isCooldown = baseBuffDef.isCooldown;
                    buffDef.isHidden = baseBuffDef.isHidden;
                    buffDef.flags = baseBuffDef.flags;
                }

                string qualityIconTextureAssetPath = Path.Combine(currentDirectory, $"tex{buffName}.png");
                buffDef.iconSprite = AssetDatabase.LoadAssetAtPath<Sprite>(qualityIconTextureAssetPath);
                if (!buffDef.iconSprite && baseIconTexture)
                {
                    Texture2D qualityIconTexture = QualityCatalog.CreateQualityIconTexture(baseIconTexture, qualityTier, baseBuffDef ? baseBuffDef.buffColor : Color.white);

                    File.WriteAllBytes(qualityIconTextureAssetPath, qualityIconTexture.EncodeToPNG());

                    AssetDatabase.ImportAsset(qualityIconTextureAssetPath);

                    TextureImporter textureImporter = (TextureImporter)AssetImporter.GetAtPath(qualityIconTextureAssetPath);
                    textureImporter.textureType = TextureImporterType.Sprite;
                    textureImporter.spritePixelsPerUnit = qualityIconTexture.width / 10.24f;
                    textureImporter.alphaIsTransparency = true;

                    AssetDatabase.ImportAsset(qualityIconTextureAssetPath, ImportAssetOptions.ForceUpdate);

                    buffDef.iconSprite = AssetDatabase.LoadAssetAtPath<Sprite>(qualityIconTextureAssetPath);
                }

                AssetDatabase.CreateAsset(buffDef, Path.Combine(currentDirectory, buffDef.name + ".asset"));

                return buffDef;
            }

            if (!_uncommonBuff)
            {
                _uncommonBuff = createBuffDef(QualityTier.Uncommon);
            }

            if (!_rareBuff)
            {
                _rareBuff = createBuffDef(QualityTier.Rare);
            }

            if (!_epicBuff)
            {
                _epicBuff = createBuffDef(QualityTier.Epic);
            }

            if (!_legendaryBuff)
            {
                _legendaryBuff = createBuffDef(QualityTier.Legendary);
            }
        }
#endif
    }
}
