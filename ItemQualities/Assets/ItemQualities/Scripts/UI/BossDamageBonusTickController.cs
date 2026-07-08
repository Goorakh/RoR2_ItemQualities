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

        private int _displayedNumber;
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

        private void OnEnable()
        {
            refreshDisplayedNumber();
        }

        private void refreshDisplayedNumber()
        {
            Image.sprite = ArrayUtils.GetSafe(NumberSprites, DisplayedNumber - 1);
        }
    }
}
