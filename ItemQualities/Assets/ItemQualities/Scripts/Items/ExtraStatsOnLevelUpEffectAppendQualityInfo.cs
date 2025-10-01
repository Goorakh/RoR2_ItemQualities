using HG;
using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using RoR2;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ItemQualities.Items
{
    public class ExtraStatsOnLevelUpEffectAppendQualityInfo : MonoBehaviour
    {
        [SystemInitializer]
        static void Init()
        {
            AddressableUtil.LoadTempAssetAsync<GameObject>(RoR2BepInExPack.GameAssetPathsBetter.RoR2_DLC2_Items_ExtraStatsOnLevelUp.ExtraStatsOnLevelUpScrapEffect_prefab).OnSuccess(scrapEffect =>
            {
                scrapEffect.EnsureComponent<ExtraStatsOnLevelUpEffectAppendQualityInfo>();
            });
        }

        EffectComponent _effectComponent;
        MultiTextRiserController _textRiserController;

        readonly List<string> _appendedTextRiserStrings = new List<string>();

        void Awake()
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
        }

        void OnEnable()
        {
            StartCoroutine(appendQualityInfo());
        }

        void OnDisable()
        {
            StopAllCoroutines();

            if (_appendedTextRiserStrings.Count > 0)
            {
                List<string> textRiserStrings = _textRiserController.DisplayStrings.ToList();

                foreach (string appendedString in _appendedTextRiserStrings)
                {
                    textRiserStrings.Remove(appendedString);
                }

                _textRiserController.DisplayStrings = textRiserStrings.ToArray();

                _appendedTextRiserStrings.Clear();
            }
        }

        IEnumerator appendQualityInfo()
        {
            yield return null;

            (int playerLevelBonus, int ambientLevelPenalty) = ExtraStatsOnLevelUp.UnpackLevelBonuses(_effectComponent.effectData.genericUInt);

            if (playerLevelBonus > 0)
            {
                addTextRiserString(Language.GetStringFormatted("EXTRASTATSONLEVELUP_INCREASE_LEVEL", playerLevelBonus));
            }

            if (ambientLevelPenalty > 0)
            {
                addTextRiserString(Language.GetStringFormatted("EXTRASTATSONLEVELUP_REDUCE_AMBIENT_LEVEL", ambientLevelPenalty));
            }
        }

        void addTextRiserString(string str)
        {
            _appendedTextRiserStrings.Add(str);
            ArrayUtils.ArrayAppend(ref _textRiserController.DisplayStrings, str);
        }
    }
}
