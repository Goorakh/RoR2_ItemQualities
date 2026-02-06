using UnityEngine;

namespace ItemQualities
{
    [DisallowMultipleComponent]
    public sealed class QualityTierContext : MonoBehaviour
    {
        public QualityTier QualityTier = QualityTier.None;

        void Awake()
        {
            ComponentCache.Add(gameObject, this);
        }

        void OnDestroy()
        {
            ComponentCache.Remove(gameObject, this);
        }
    }
}
