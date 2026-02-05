using RoR2;
using UnityEngine;

namespace ItemQualities
{
    internal sealed class InteractableDef
    {
        public GameObject Prefab;

        public InteractableSpawnCard SpawnCard;

        public bool CanCopy = true;

        public InteractableInfoProvider PrefabInfoProviderComponent { get; }

        public string Name { get; }

        public int InteractableIndex => PrefabInfoProviderComponent ? PrefabInfoProviderComponent.CatalogIndex : -1;

        public InteractableDef(GameObject prefab)
        {
            Prefab = prefab;
            Name = Prefab ? Prefab.name : string.Empty;
            PrefabInfoProviderComponent = Prefab ? Prefab.GetComponent<InteractableInfoProvider>() : null;
        }

        public override string ToString()
        {
            return Name;
        }

        public override int GetHashCode()
        {
            return Name.GetHashCode();
        }
    }
}
