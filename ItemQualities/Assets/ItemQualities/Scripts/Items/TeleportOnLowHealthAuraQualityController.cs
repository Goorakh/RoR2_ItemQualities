using RoR2;
using UnityEngine;

namespace ItemQualities.Items
{
    [DefaultExecutionOrder(50)]
    public class TeleportOnLowHealthAuraQualityController : MonoBehaviour
    {
        TeleportOnLowHealthAuraController _auraController;

        MemoizedGetComponent<CharacterBody> _ownerBody;

        Vector3 _baseScale = Vector3.one;

        void Awake()
        {
            _auraController = GetComponent<TeleportOnLowHealthAuraController>();
        }

        void Start()
        {
            _baseScale = transform.localScale;

            Inventory.onInventoryChangedGlobal += onInventoryChangedGlobal;
            refreshScale();
        }

        void onInventoryChangedGlobal(Inventory inventory)
        {
            CharacterBody ownerBody = _ownerBody.Get(_auraController ? _auraController.owner : null);
            if (ownerBody && ownerBody.inventory == inventory)
            {
                refreshScale();
            }
        }

        void refreshScale()
        {
            CharacterBody ownerBody = _ownerBody.Get(_auraController ? _auraController.owner : null);
            Inventory ownerInventory = ownerBody ? ownerBody.inventory : null;

            ItemQualityCounts teleportOnLowHealth = ItemQualitiesContent.ItemQualityGroups.TeleportOnLowHealth.GetItemCountsEffective(ownerInventory);
            if (teleportOnLowHealth.TotalQualityCount > 0)
            {
                float auraScaleMult = 1f;
                auraScaleMult += 0.2f * teleportOnLowHealth.UncommonCount;
                auraScaleMult += 0.4f * teleportOnLowHealth.RareCount;
                auraScaleMult += 0.7f * teleportOnLowHealth.EpicCount;
                auraScaleMult += 1.0f * teleportOnLowHealth.LegendaryCount;

                transform.localScale = _baseScale * Mathf.Max(1f, auraScaleMult);
            }
        }
    }
}
