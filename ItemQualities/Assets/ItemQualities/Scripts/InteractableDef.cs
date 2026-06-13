using RoR2;
using UnityEngine;

namespace ItemQualities
{
    internal sealed class InteractableDef
    {
        public readonly GameObject Prefab;

        public readonly InteractableSpawnCard SpawnCard;

        public bool CanCopy = true;

        public InteractableInfoProvider PrefabInfoProviderComponent { get; }

        public IInspectInfoProvider PrefabInspectInfoProvider { get; }

        public GenericInspectInfoProvider PrefabGenericInspectInfoProvider { get; }

        public IDisplayNameProvider PrefabDisplayNameProvider { get; }

        public SpecialObjectAttributes PrefabSpecialObjectAttributes { get; }

        public PingInfoProvider PrefabPingInfoProvider { get; }

        public string Name { get; }

        public int InteractableIndex => PrefabInfoProviderComponent ? PrefabInfoProviderComponent.CatalogIndex : -1;

        public InteractableDef(InteractableSpawnCard spawnCard)
        {
            SpawnCard = spawnCard;

            Prefab = SpawnCard.prefab;
            Name = Prefab.name;

            PrefabInfoProviderComponent = Prefab.GetComponent<InteractableInfoProvider>();
            PrefabInspectInfoProvider = Prefab.GetComponent<IInspectInfoProvider>();
            PrefabGenericInspectInfoProvider = Prefab.GetComponent<GenericInspectInfoProvider>();
            PrefabDisplayNameProvider = Prefab.GetComponent<IDisplayNameProvider>();
            PrefabSpecialObjectAttributes = Prefab.GetComponent<SpecialObjectAttributes>();
            PrefabPingInfoProvider = Prefab.GetComponent<PingInfoProvider>();
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
