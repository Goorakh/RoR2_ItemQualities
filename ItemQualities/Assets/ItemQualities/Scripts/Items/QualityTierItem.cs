using ItemQualities.Utilities.Extensions;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using RoR2;
using System;
using System.Reflection;
using UnityEngine;

namespace ItemQualities.Items
{
    internal static class QualityTierItem
    {
        private static readonly int _overrideQualityRampIndex = -1;

        [SystemInitializer]
        private static void Init()
        {
            On.RoR2.Util.GetBestBodyName += Util_GetBestBodyName;

            MethodInfo characterModelUpdateMaterialsMethod = typeof(CharacterModel).GetMethod(nameof(CharacterModel.UpdateMaterials), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            if (characterModelUpdateMaterialsMethod != null)
            {
                // EliteAPI compat: Apply our hook before EliteRamp hook so that our call happens after EliteAPI to prevent it from overriding our ramps
                new ILHook(characterModelUpdateMaterialsMethod, CharacterModel_UpdateMaterials, new ILHookConfig
                {
                    Priority = -100
                });
            }
            else
            {
                Log.Error("Failed to find CharacterModel.UpdateMaterials method");
            }

            MasterSummon.onServerMasterSummonGlobal += onServerMasterSummonGlobal;
        }

        private static void onServerMasterSummonGlobal(MasterSummon.MasterSummonReport summonReport)
        {
            if (!summonReport.summonMasterInstance || !summonReport.summonMasterInstance.inventory)
                return;

            GameObject summonerBodyObject = summonReport.masterSummon?.summonerBodyObject;
            CharacterBody summonerBody = summonerBodyObject ? summonerBodyObject.GetComponent<CharacterBody>() : null;
            if (!summonerBody || !summonerBody.inventory)
                return;

            ItemQualityCounts qualityTierCounts = summonReport.masterSummon.summonerBodyObject.GetComponent<CharacterBody>().inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.QualityTier);
            QualityTier summonerQualityTier = qualityTierCounts.HighestQuality;
            if (summonerQualityTier != QualityTier.None)
            {
                summonReport.summonMasterInstance.inventory.GiveItemPermanent(ItemQualitiesContent.ItemQualityGroups.QualityTier.GetItemIndex(summonerQualityTier));
            }
        }

        private static string Util_GetBestBodyName(On.RoR2.Util.orig_GetBestBodyName orig, GameObject bodyObject)
        {
            string bodyName = orig(bodyObject);

            if (bodyObject && bodyObject.TryGetComponent(out CharacterBody body) && body.inventory)
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

        private static void CharacterModel_UpdateMaterials(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            /*
             *  // this.propertyStorage.SetFloat(CommonShaderProperties._EliteIndex, (float)(this.shaderEliteRampIndex + 1));
             *  IL_0201: ldarg.0
             *  IL_0202: ldfld     class [UnityEngine.CoreModule]UnityEngine.MaterialPropertyBlock RoR2.CharacterModel::propertyStorage
             *  IL_0207: ldsfld    int32 RoR2.CommonShaderProperties::_EliteIndex
             *  IL_020C: ldarg.0
             *  IL_020D: ldfld     int32 RoR2.CharacterModel::shaderEliteRampIndex
             *  IL_0212: ldc.i4.1
             *  IL_0213: add
             *  IL_0214: conv.r4
             *  IL_0215: callvirt  instance void [UnityEngine.CoreModule]UnityEngine.MaterialPropertyBlock::SetFloat(int32, float32)
             */

            int patchCount = 0;

            while (c.TryFindNext(out ILCursor[] foundCursors,
                                 x => x.MatchLdfld<CharacterModel>(nameof(CharacterModel.shaderEliteRampIndex)),
                                 x => x.MatchCallOrCallvirt<MaterialPropertyBlock>(nameof(MaterialPropertyBlock.SetFloat))))
            {
                // IL_0215: callvirt  instance void [UnityEngine.CoreModule]UnityEngine.MaterialPropertyBlock::SetFloat(int32, float32)
                c.Goto(foundCursors[1].Next, MoveType.After);

                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate<Action<CharacterModel>>(handleQualityRamp);

                static void handleQualityRamp(CharacterModel characterModel)
                {
                    QualityTier qualityTier = QualityTier.None;
                    if (characterModel.body && characterModel.body.inventory)
                    {
                        qualityTier = characterModel.body.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.QualityTier).HighestQuality;
                    }

                    MaterialPropertyBlock propertyStorage = characterModel.propertyStorage;

                    if (qualityTier != QualityTier.None)
                    {
                        propertyStorage.SetTexture(ShaderProperties._EliteRamp, QualityCatalog.GetQualityTierDef(qualityTier).colorRampTexture);
                        propertyStorage.SetFloat(CommonShaderProperties._EliteIndex, 1); // Force enable elite ramp
                    }
                    else
                    {
                        Texture eliteRamp = propertyStorage.GetTexture(ShaderProperties._EliteRamp);
                        if (QualityCatalog.IsQualityRampTexture(eliteRamp))
                        {
                            propertyStorage.SetTexture(ShaderProperties._EliteRamp, CommonTextures.DefaultElitesRamp);
                        }
                    }
                }

                patchCount++;
            }
            
            if (patchCount == 0)
            {
                Log.PatchError(il, "Failed to find patch location");
            }
            else
            {
                Log.Debug($"Found {patchCount} patch location(s)");
            }
        }
    }
}
