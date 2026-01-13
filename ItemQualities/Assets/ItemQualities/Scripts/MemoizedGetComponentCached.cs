using ItemQualities.Utilities.Extensions;
using UnityEngine;

namespace ItemQualities
{
    internal struct MemoizedGetComponentCached<TComponent> where TComponent : Component
    {
        GameObject _cachedGameObject;
        TComponent _cachedComponent;

        public TComponent Get(GameObject gameObject)
        {
            if (_cachedGameObject != gameObject)
            {
                _cachedGameObject = gameObject;
                _cachedComponent = gameObject ? gameObject.GetComponentCached<TComponent>() : null;
            }

            return _cachedComponent;
        }

        public bool TryGet(GameObject gameObject, out TComponent result)
        {
            result = Get(gameObject);
            return result;
        }
    }
}
