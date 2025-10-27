using RoR2;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace ItemQualities.Items
{
    public sealed class MushroomVoidQualityItemBehavior : MonoBehaviour
    {
        public const float HealOrbSpawnInterval = 1f;

        CharacterBody _body;

        float _healOrbSpawnTimer;

        void Awake()
        {
            _body = GetComponent<CharacterBody>();
        }

        void OnEnable()
        {
            _healOrbSpawnTimer = 0f;
        }

        void FixedUpdate()
        {
            if (_body.isSprinting)
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
            Vector3 spawnPosition = _body.corePosition;

            const float SpawnDelay = 0.2f;
            yield return new WaitForSeconds(SpawnDelay);

            ItemQualityCounts mushroomVoid = ItemQualitiesContent.ItemQualityGroups.MushroomVoid.GetItemCounts(_body.inventory);

            int healPackSizeBase = (1 * mushroomVoid.UncommonCount) +
                                   (2 * mushroomVoid.RareCount) +
                                   (3 * mushroomVoid.EpicCount) +
                                   (4 * mushroomVoid.LegendaryCount);
            float healPackSize = Mathf.Pow(healPackSizeBase, 0.25f);

            float flatHealing = _body.maxHealth * ((0.01f * mushroomVoid.UncommonCount) +
                                                   (0.03f * mushroomVoid.RareCount) +
                                                   (0.05f * mushroomVoid.EpicCount) +
                                                   (0.10f * mushroomVoid.LegendaryCount));
            float fractionalHealing = 0f;

            GameObject healPackObj = Instantiate(ItemQualitiesContent.NetworkedPrefabs.HealPackDelayed, spawnPosition, Quaternion.identity);

            healPackObj.transform.localScale = new Vector3(healPackSize, healPackSize, healPackSize);

            if (healPackObj.TryGetComponent(out TeamFilter teamFilter))
            {
                teamFilter.teamIndex = _body.teamComponent.teamIndex;
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
                if (_body.moveSpeed > 0)
                {
                    estimatedTimeToLeavePickupRadius = (gravitateSize / _body.moveSpeed);
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
