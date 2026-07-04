using ItemQualities.Utilities.Extensions;
using RoR2;
using UnityEngine;

namespace ItemQualities
{
    internal static class QualityItemDisplays
    {
        [InitDuringStartupPhase(GameInitPhase.DuringIntro)]
        private static void Init()
        {
            On.RoR2.CharacterModel.UpdateMaterials += CharacterModel_UpdateMaterials;
        }

        private static void CharacterModel_UpdateMaterials(On.RoR2.CharacterModel.orig_UpdateMaterials orig, CharacterModel self)
        {
            orig(self);

            Inventory inventory = self.body ? self.body.inventory : null;
            bool shouldDisplayQualityTier = inventory && !self.body.isPlayerControlled;

            foreach (CharacterModel.ParentedPrefabDisplay prefabDisplay in self.parentedPrefabDisplays)
            {
                QualityTier prefabDisplayQualityTier;
                if (prefabDisplay.itemIndex != ItemIndex.None)
                {
                    ItemQualityGroupIndex itemGroupIndex = QualityCatalog.FindItemQualityGroupIndex(prefabDisplay.itemIndex);
                    if (itemGroupIndex == ItemQualityGroupIndex.Invalid)
                    {
                        continue;
                    }

                    prefabDisplayQualityTier = inventory ? inventory.CalculateEffectiveItemStacks(itemGroupIndex).HighestQuality : QualityTier.None;
                }
                else if (prefabDisplay.equipmentIndex != EquipmentIndex.None)
                {
                    EquipmentQualityGroupIndex equipmentGroupIndex = QualityCatalog.FindEquipmentQualityGroupIndex(prefabDisplay.equipmentIndex);
                    if (equipmentGroupIndex == EquipmentQualityGroupIndex.Invalid)
                    {
                        continue;
                    }

                    prefabDisplayQualityTier = inventory ? inventory.GetActiveEquipmentQualityTier() : QualityTier.None;
                }
                else
                {
                    continue;
                }

                if (!shouldDisplayQualityTier)
                {
                    prefabDisplayQualityTier = QualityTier.None;
                }

                foreach (CharacterModel.RendererInfo rendererInfo in prefabDisplay.itemDisplay.rendererInfos)
                {
                    if (rendererInfo.renderer)
                    {
                        MaterialPropertyBlock propertyStorage = self.propertyStorage;

                        rendererInfo.renderer.GetPropertyBlock(propertyStorage);

                        if (prefabDisplayQualityTier != QualityTier.None)
                        {
                            propertyStorage.SetTexture(ShaderProperties._EliteRamp, QualityCatalog.GetQualityTierDef(prefabDisplayQualityTier).colorRampTexture);
                            propertyStorage.SetFloat(CommonShaderProperties._EliteIndex, 1); // Force enable elite ramp
                        }
                        else if (QualityCatalog.IsQualityRampTexture(propertyStorage.GetTexture(ShaderProperties._EliteRamp)))
                        {
                            propertyStorage.SetTexture(ShaderProperties._EliteRamp, CommonTextures.DefaultElitesRamp);
                            propertyStorage.SetFloat(CommonShaderProperties._EliteIndex, 0); // Disable elite ramp
                        }

                        rendererInfo.renderer.SetPropertyBlock(propertyStorage);
                    }
                }
            }
        }
    }
}
