using ItemQualities.Utilities.Extensions;
using UnityEngine;

namespace ItemQualities
{
    [DisallowMultipleComponent]
    public sealed class QualityTierContext : MonoBehaviour
    {
        public QualityTier QualityTier = QualityTier.None;

        private void Awake()
        {
            ComponentCache.Add(gameObject, this);
        }

        private void OnDestroy()
        {
            ComponentCache.Remove(gameObject, this);
        }

        public static QualityTier GetQualityTier(GameObject gameObject)
        {
            return gameObject && gameObject.TryGetComponentCached(out QualityTierContext context) ? context.QualityTier : QualityTier.None;
        }
    }
}
