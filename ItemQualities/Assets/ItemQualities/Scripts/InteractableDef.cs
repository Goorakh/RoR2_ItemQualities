using RoR2;
using UnityEngine;

namespace ItemQualities
{
    internal sealed class InteractableDef
    {
        public int InteractableIndex = -1;

        public GameObject Prefab;

        public InteractableSpawnCard SpawnCard;

        public bool CanCopy = true;

        public string Name { get; }

        public InteractableDef(GameObject prefab)
        {
            Prefab = prefab;
            Name = Prefab ? Prefab.name : string.Empty;
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
