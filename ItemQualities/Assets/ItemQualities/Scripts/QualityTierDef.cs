using UnityEngine;

namespace ItemQualities
{
    [CreateAssetMenu(menuName = "ItemQualities/QualityTierDef")]
    public sealed class QualityTierDef : ScriptableObject
    {
        public QualityTier qualityTier = QualityTier.None;

        public Color color;

        public Sprite icon;

        public Sprite consumedIcon;

        public GameObject ChestOpenEffectPrefab;

        public string pickupDropSoundEventName = string.Empty;

        public string pickupLandSoundEventName = string.Empty;
    }
}
