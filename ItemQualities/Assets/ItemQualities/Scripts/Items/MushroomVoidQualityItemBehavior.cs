using RoR2;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace ItemQualities.Items
{
    public sealed class MushroomVoidQualityItemBehavior : QualityItemBodyBehavior
    {
        [ItemGroupAssociation(QualityItemBehaviorUsageFlags.Server)]
        static ItemQualityGroup GetItemGroup()
        {
            return ItemQualitiesContent.ItemQualityGroups.MushroomVoid;
        }

        public const float HealOrbSpawnInterval = 1f;

        float _healOrbSpawnTimer;

        void OnEnable()
        {
            _healOrbSpawnTimer = 0f;
        }

        void FixedUpdate()
        {
            if (Body.isSprinting)
            {
                _healOrbSpawnTimer += Time.fixedDeltaTime;
                if (_healOrbSpawnTimer >= HealOrbSpawnInterval)
                {
                    _healOrbSpawnTimer -= HealOrbSpawnInterval;
                    StartCoroutine(spawnHealOrb());
                }
            }
            else
            {
                _healOrbSpawnTimer = 0f;
            }
        }

        IEnumerator spawnHealOrb()
        {
            Vector3 spawnPosition = Body.corePosition;

            const float SpawnDelay = 0.2f;
            yield return new WaitForSeconds(SpawnDelay);

            ItemQualityCounts mushroomVoid = Stacks;
            if (mushroomVoid.TotalQualityCount <= 0)
                yield break;

            int healPackSizeBase = (1 * mushroomVoid.UncommonCount) +
                                   (2 * mushroomVoid.RareCount) +
                                   (3 * mushroomVoid.EpicCount) +
                                   (4 * mushroomVoid.LegendaryCount);

            float healPackSize = Mathf.Pow(healPackSizeBase, 0.25f);

            float flatHealing = Body.maxHealth * ((0.01f * mushroomVoid.UncommonCount) +
                                                   (0.03f * mushroomVoid.RareCount) +
                                                   (0.05f * mushroomVoid.EpicCount) +
                                                   (0.10f * mushroomVoid.LegendaryCount));
            float fractionalHealing = 0f;

            GameObject healPackObj = Instantiate(ItemQualitiesContent.NetworkedPrefabs.HealPackDelayed, spawnPosition, Quaternion.identity);

            healPackObj.transform.localScale = new Vector3(healPackSize, healPackSize, healPackSize);

            if (healPackObj.TryGetComponent(out TeamFilter teamFilter))
            {
                teamFilter.teamIndex = Body.teamComponent.teamIndex;
            }

            HealthPickup healthPickup = healPackObj.GetComponentInChildren<HealthPickup>(true);
            if (healthPickup)
            {
                healthPickup.flatHealing = flatHealing;
                healthPickup.fractionalHealing = fractionalHealing;
            }

            if (healPackObj.TryGetComponent(out DelayedHealPackController delayedHealPackController))
            {
                float gravitateSize = 6f;

                GravitatePickup gravitatePickup = healPackObj.GetComponentInChildren<GravitatePickup>(true);
                if (gravitatePickup && gravitatePickup.TryGetComponent(out SphereCollider gravitatePickupTrigger))
                {
                    gravitateSize = gravitatePickupTrigger.radius * gravitatePickupTrigger.transform.lossyScale.x;
                }

                float estimatedTimeToLeavePickupRadius;
                if (Body.moveSpeed > 0)
                {
                    estimatedTimeToLeavePickupRadius = (gravitateSize / Body.moveSpeed);
                }
                else
                {
                    estimatedTimeToLeavePickupRadius = 1f;
                }

                estimatedTimeToLeavePickupRadius -= SpawnDelay;

                delayedHealPackController.Delay = Mathf.Clamp(estimatedTimeToLeavePickupRadius + 0.2f, 0.5f, 3f);
            }

            NetworkServer.Spawn(healPackObj);
        }
    }
}
