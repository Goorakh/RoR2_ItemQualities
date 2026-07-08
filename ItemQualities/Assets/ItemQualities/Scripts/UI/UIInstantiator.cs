using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using RoR2;
using RoR2.UI;
using RoR2BepInExPack.GameAssetPathsBetter;
using System.Linq;
using UnityEngine;

namespace ItemQualities.UI
{
    internal sealed class UIInstantiator : MonoBehaviour
    {
        [SystemInitializer]
        private static void Init()
        {
            AddressableUtil.LoadAssetAsync<GameObject>(RoR2_Base_UI.HUDSimple_prefab).OnSuccess(hudPrefab =>
            {
                hudPrefab.AddComponent<UIInstantiator>();
            });
        }

        private void Awake()
        {
            HUD hud = GetComponent<HUD>();

            ChildLocator childLocator = GetComponent<ChildLocator>();

            Transform bottomRightClusterTransform = childLocator.FindChild("BottomRightCluster");

            Instantiate(ItemQualitiesContent.Prefabs.HitlistMarkersUI, bottomRightClusterTransform);

            EquipmentIcon mainEquipmentIcon = hud.equipmentIcons.FirstOrDefault(e => !e.displayAlternateEquipment);
            if (mainEquipmentIcon && mainEquipmentIcon.displayRoot)
            {
                GameObject parryProjectileDisplayUI = Instantiate(ItemQualitiesContent.Prefabs.ParryProjectileDisplayUI, mainEquipmentIcon.displayRoot.transform);
                parryProjectileDisplayUI.transform.SetAsFirstSibling();

                ParryStoredProjectileDisplay parryProjectileDisplay = parryProjectileDisplayUI.GetComponent<ParryStoredProjectileDisplay>();
                parryProjectileDisplay.ParentEquipmentIcon = mainEquipmentIcon;
            }

            enabled = false;
        }
    }
}
