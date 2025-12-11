using RoR2;
using RoR2.Projectile;
using UnityEngine;
using UnityEngine.Networking;

namespace ItemQualities
{
    [RequireComponent(typeof(ProjectileController))]
    public sealed class ProjectileInstantiateDeployableOnImpact : MonoBehaviour, IProjectileImpactBehavior
    {
        ProjectileController _projectileController;

        public DeployableSlot DeployableSlot = DeployableSlot.None;

        public GameObject DeployablePrefab;

        void Awake()
        {
            _projectileController = GetComponent<ProjectileController>();
        }

        void IProjectileImpactBehavior.OnProjectileImpact(ProjectileImpactInfo impactInfo)
        {
            if (!NetworkServer.active)
            {
                Log.Warning("Called on client");
                return;
            }

            CharacterBody ownerBody = _projectileController.owner ? _projectileController.owner.GetComponent<CharacterBody>() : null;
            if (!ownerBody || !ownerBody.master)
                return;

            Vector3 spawnPosition = transform.position;

            Vector3 forward = Vector3.ProjectOnPlane(Random.onUnitSphere, impactInfo.estimatedImpactNormal).normalized;
            if (forward.sqrMagnitude < 0.01f)
            {
                forward = Vector3.ProjectOnPlane(Vector3.right, impactInfo.estimatedImpactNormal).normalized;
            }

            Quaternion spawnRotation = Quaternion.LookRotation(forward, impactInfo.estimatedImpactNormal);

            GameObject deployableObj = Instantiate(DeployablePrefab, spawnPosition, spawnRotation);

            if (deployableObj.TryGetComponent(out GenericOwnership genericOwnership))
            {
                genericOwnership.ownerObject = _projectileController.owner;
            }

            NetworkServer.Spawn(deployableObj);

            ownerBody.master.AddDeployable(deployableObj.GetComponent<Deployable>(), DeployableSlot);

            Destroy(gameObject);
        }
    }
}
