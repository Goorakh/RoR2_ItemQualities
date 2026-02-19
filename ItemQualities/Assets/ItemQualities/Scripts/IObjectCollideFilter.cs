using System;

namespace ItemQualities
{
    public interface IObjectCollideFilter
    {
        event Action<ObjectCollisionManager> OnFilterDirty;

        bool PassesFilter(ObjectCollisionManager collisionManager);
    }
}
