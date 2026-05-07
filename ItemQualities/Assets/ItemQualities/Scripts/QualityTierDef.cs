using RoR2;
using UnityEngine;

namespace ItemQualities
{
    [CreateAssetMenu(menuName = "ItemQualities/QualityTierDef")]
    public sealed class QualityTierDef : ScriptableObject
    {
        public QualityTier qualityTier = QualityTier.None;

        public string modifierToken = string.Empty;

        public string consumedModifierToken = string.Empty;

        public Color color;

        public Texture2D colorRampTexture;

        public Sprite icon;

        public Sprite consumedIcon;

        public GameObject ChestOpenEffectPrefab;

        public NetworkSoundEventDef pickupDropSound;

        public NetworkSoundEventDef pickupLandSound;
    }
}
