using RoR2;
using UnityEngine;

namespace ItemQualities.Utilities
{
    internal static class MasterUtil
    {
        /// <summary>
        /// Gets the core position of a master's body if it exists, an estimate of the death core position if it is dead, and an estimate for the to-be position if the body is not yet spawned
        /// </summary>
        /// <returns></returns>
        public static Vector3 GetBestBodyCorePosition(CharacterMaster master)
        {
            CharacterBody body = master.GetBody();
            if (body)
            {
                return body.corePosition;
            }

            if (master.lostBodyToDeath)
            {
                Vector3 deathPosition = master.deathFootPosition;

                if (master.bodyPrefab && master.bodyPrefab.TryGetComponent(out CapsuleCollider bodyCapsule))
                {
                    Vector3 localFootPosition = bodyCapsule.transform.InverseTransformPoint(deathPosition);

                    localFootPosition += bodyCapsule.center;
                    localFootPosition.y += bodyCapsule.height * 0.5f;

                    deathPosition = bodyCapsule.transform.TransformPoint(localFootPosition);
                }

                return deathPosition;
            }

            return GetSpawnCorePosition(master, master.transform.position);
        }

        public static Vector3 GetSpawnCorePosition(CharacterMaster master, Vector3 spawnPosition)
        {
            if (master.bodyPrefab && master.bodyPrefab.TryGetComponent(out CapsuleCollider bodyCapsule))
            {
                Vector3 localSpawnPosition = bodyCapsule.transform.InverseTransformPoint(spawnPosition);

                localSpawnPosition += bodyCapsule.center;

                spawnPosition = bodyCapsule.transform.TransformPoint(localSpawnPosition);
            }

            return spawnPosition;
        }
    }
}
