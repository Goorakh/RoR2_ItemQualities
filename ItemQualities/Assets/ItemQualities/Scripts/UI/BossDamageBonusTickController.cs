using HG;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace ItemQualities.UI
{
    public sealed class BossDamageBonusTickController : MonoBehaviour
    {
        public Sprite[] NumberSprites = Array.Empty<Sprite>();

        public Image Image;

        int _displayedNumber;
        public int DisplayedNumber
        {
            get
            {
                return _displayedNumber;
            }
            set
            {
                if (_displayedNumber == value)
                    return;

                _displayedNumber = value;
                refreshDisplayedNumber();
            }
        }

        void OnEnable()
        {
            refreshDisplayedNumber();
        }

        void refreshDisplayedNumber()
        {
            Image.sprite = ArrayUtils.GetSafe(NumberSprites, DisplayedNumber - 1);
        }
    }
}
