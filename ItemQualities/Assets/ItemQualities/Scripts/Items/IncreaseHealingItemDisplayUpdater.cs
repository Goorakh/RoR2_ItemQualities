using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using RoR2;
using RoR2BepInExPack.GameAssetPathsBetter;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace ItemQualities
{
    public class IncreaseHealingItemDisplayUpdater : MonoBehaviour
    {
        private static int _immuneMatPos;

        public MeshRenderer meshRenderer;

        private CharacterBodyExtraStatsTracker _bodyStats;

        [SystemInitializer]
        private static IEnumerator Init()
        {
            AsyncOperationHandle<Material> matImmuneHandle = AddressableUtil.LoadAssetAsync<Material>(RoR2_Base_Common.matImmune_mat);

            yield return matImmuneHandle;

            if (!matImmuneHandle.AssertLoaded("matImmune"))
                yield break;

            AddressableUtil.LoadAssetAsync<GameObject>(RoR2_Base_IncreaseHealing.DisplayAntler_prefab).OnSuccess(AntlerDisplay =>
            {
                IncreaseHealingItemDisplayUpdater displayUpdater = AntlerDisplay.AddComponent<IncreaseHealingItemDisplayUpdater>();
                Transform antlerMeshDual = AntlerDisplay.transform.Find("AntlerMeshDual");
                if (antlerMeshDual.TryGetComponent(out MeshRenderer meshRenderer))
                {
                    displayUpdater.meshRenderer = meshRenderer;
                    List<Material> materials = new List<Material>();
                    meshRenderer.GetSharedMaterials(materials);
                    _immuneMatPos = materials.Count;
                    materials.Add(new Material(matImmuneHandle.Result));
                    meshRenderer.SetSharedMaterials(materials);
                }
            });
        }

        private void Start()
        {
            if (!meshRenderer)
                return;
            CharacterModel characterModel = GetComponentInParent<CharacterModel>();
            if (characterModel)
            {
                CharacterBody body = characterModel.body;
                if (body)
                {
                    _bodyStats = body.GetComponentCached<CharacterBodyExtraStatsTracker>();
                }
                meshRenderer.materials[_immuneMatPos].SetFloat("_AlphaBoost", 0f);
                meshRenderer.materials[_immuneMatPos].SetFloat("_OffsetAmount", 0.5f);
            }
        }

        private void FixedUpdate()
        {
            if (!meshRenderer || !_bodyStats)
                return;

            float indicatorAlpha = _bodyStats.IncreaseHealingPreImmunityIndicator ? 1 : 0;
            meshRenderer.materials[_immuneMatPos].SetFloat("_AlphaBoost", indicatorAlpha);
        }
    }
}
