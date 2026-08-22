using HG;
using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using RoR2;
using RoR2BepInExPack.GameAssetPathsBetter;
using System.Collections.Generic;
using UnityEngine;

namespace ItemQualities.Items
{
    public sealed class ExtraStatsOnLevelUpEffectAppendQualityInfo : MonoBehaviour
    {
        [SystemInitializer]
        private static void Init()
        {
            AddressableUtil.LoadAssetAsync<GameObject>(RoR2_DLC2_Items_ExtraStatsOnLevelUp.ExtraStatsOnLevelUpScrapEffect_prefab).OnSuccess(scrapEffect =>
            {
                scrapEffect.EnsureComponent<ExtraStatsOnLevelUpEffectAppendQualityInfo>();
            });
        }

        private EffectComponent _effectComponent;
        private MultiTextRiserController _textRiserController;

        private List<string> _appendedTextRiserStrings;

        private void Awake()
        {
            _effectComponent = GetComponent<EffectComponent>();
            _textRiserController = GetComponent<MultiTextRiserController>();

            if (!_effectComponent)
            {
                Log.Warning($"Missing EffectComponent on {Util.GetGameObjectHierarchyName(gameObject)}");
                enabled = false;
                return;
            }

            if (!_textRiserController)
            {
                Log.Warning($"Missing MultiTextRiserController on {Util.GetGameObjectHierarchyName(gameObject)}");
                enabled = false;
                return;
            }

            _effectComponent.OnEffectComponentReset += onReset;

            _appendedTextRiserStrings = ListPool<string>.RentCollection();
        }

        private void OnDestroy()
        {
            _effectComponent.OnEffectComponentReset -= onReset;

            _appendedTextRiserStrings = ListPool<string>.ReturnCollection(_appendedTextRiserStrings);
        }

        private void onReset(bool hasEffectData)
        {
            if (_appendedTextRiserStrings == null)
                return;

            if (_appendedTextRiserStrings.Count > 0)
            {
                using var _ = ListPool<string>.RentCollection(out List<string> textRiserStrings);
                textRiserStrings.AddRange(_textRiserController.DisplayStrings);

                foreach (string appendedString in _appendedTextRiserStrings)
                {
                    textRiserStrings.Remove(appendedString);
                }

                if (textRiserStrings.Count < _textRiserController.DisplayStrings.Length)
                {
                    _textRiserController.DisplayStrings = textRiserStrings.ToArray();
                }

                _appendedTextRiserStrings.Clear();
            }

            if (hasEffectData)
            {
                (int playerLevelBonus, int ambientLevelPenalty) = ExtraStatsOnLevelUp.UnpackLevelBonuses(_effectComponent.effectData.genericUInt);

                if (playerLevelBonus > 0)
                {
                    addTextRiserString(Language.GetStringFormatted("EXTRASTATSONLEVELUP_INCREASE_LEVEL", playerLevelBonus));
                }

                if (ambientLevelPenalty > 0)
                {
                    addTextRiserString(Language.GetStringFormatted("EXTRASTATSONLEVELUP_REDUCE_AMBIENT_LEVEL", ambientLevelPenalty));
                }

                void addTextRiserString(string str)
                {
                    _appendedTextRiserStrings.Add(str);
                    ArrayUtils.ArrayAppend(ref _textRiserController.DisplayStrings, str);
                }
            }
        }
    }
}
