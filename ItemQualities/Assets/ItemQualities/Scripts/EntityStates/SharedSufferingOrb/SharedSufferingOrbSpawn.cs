using RoR2;
using UnityEngine;

using Random = UnityEngine.Random;

namespace EntityStates.SharedSufferingOrb
{
    public sealed class SharedSufferingOrbSpawn : EntityState
    {
        public override void OnEnter()
        {
            base.OnEnter();

            if (isAuthority)
            {
                Vector3 rayOrigin = characterBody.corePosition + (Vector3.up * 0.5f);
                for (int i = 0; i < 6; i++)
                {
                    if (Physics.Raycast(rayOrigin, Random.onUnitSphere, out RaycastHit hit, 10f, LayerIndex.world.mask))
                    {
                        transform.position = hit.point;
                        transform.up = hit.normal;
                        break;
                    }
                }
            }

            outer.SetNextStateToMain();
        }
    }
}
