using ItemQualities.Utilities.Extensions;
using RoR2;
using UnityEngine;

namespace ItemQualities.Items
{
    static class QualityTierItem
    {
        [SystemInitializer]
        static void Init()
        {
            On.RoR2.Util.GetBestBodyName += Util_GetBestBodyName;
        }

        static string Util_GetBestBodyName(On.RoR2.Util.orig_GetBestBodyName orig, GameObject bodyObject)
        {
            string bodyName = orig(bodyObject);

            if (bodyObject.TryGetComponent(out CharacterBody body) && body.inventory)
            {
                ItemQualityCounts qualityTierItems = body.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.QualityTier);
                QualityTier bodyQualityTier = qualityTierItems.HighestQuality;
                if (bodyQualityTier > QualityTier.None)
                {
                    QualityTierDef qualityTierDef = QualityCatalog.GetQualityTierDef(bodyQualityTier);
                    if (!string.IsNullOrWhiteSpace(qualityTierDef.modifierToken))
                    {
                        bodyName = Language.GetStringFormatted(qualityTierDef.modifierToken, bodyName);
                    }
                }
            }

            return bodyName;
        }
    }
}
