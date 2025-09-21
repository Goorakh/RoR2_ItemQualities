using UnityEngine;

namespace ItemQualities
{
    [CreateAssetMenu(menuName = "ItemQualities/QualityTierDef")]
    public class QualityTierDef : ScriptableObject
    {
        public QualityTier qualityTier = QualityTier.None;

        public Color color;

        public Sprite icon;
    }
}
