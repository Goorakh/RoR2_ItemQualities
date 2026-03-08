using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using RoR2;
using RoR2BepInExPack.GameAssetPathsBetter;
using UnityEngine;

namespace ItemQualities.UI
{
    sealed class UIInstantiator : MonoBehaviour
    {
        [SystemInitializer]
        static void Init()
        {
            AddressableUtil.LoadAssetAsync<GameObject>(RoR2_Base_UI.HUDSimple_prefab).OnSuccess(hudPrefab =>
            {
                hudPrefab.AddComponent<UIInstantiator>();
            });
        }

        void Awake()
        {
            ChildLocator childLocator = GetComponent<ChildLocator>();

            Transform bottomRightClusterTransform = childLocator.FindChild("BottomRightCluster");

            Instantiate(ItemQualitiesContent.Prefabs.HitlistMarkersUI, bottomRightClusterTransform);

            enabled = false;
        }
    }
}
