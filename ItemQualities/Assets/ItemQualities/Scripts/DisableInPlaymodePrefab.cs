using ItemQualities.ContentManagement;
using UnityEngine;

namespace ItemQualities
{
    public sealed class DisableInPlaymodePrefab : MonoBehaviour, IContentLoadCallback
    {
        void IContentLoadCallback.OnContentLoad()
        {
            gameObject.SetActive(false);
        }
    }
}
