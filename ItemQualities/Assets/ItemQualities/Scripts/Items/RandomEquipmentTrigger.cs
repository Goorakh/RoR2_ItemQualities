using ItemQualities.Utilities.Extensions;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using System;

namespace ItemQualities.Items
{
    static class RandomEquipmentTrigger
    {
        [SystemInitializer]
        static void Init()
        {
            IL.RoR2.EquipmentSlot.OnEquipmentExecuted += EquipmentSlot_OnEquipmentExecuted;
        }

        static void EquipmentSlot_OnEquipmentExecuted(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (!c.TryFindNext(out ILCursor[] foundCursors,
                               x => x.MatchLdsfld(typeof(DLC1Content.Items), nameof(DLC1Content.Items.RandomEquipmentTrigger)),
                               x => x.MatchCallOrCallvirt<EquipmentSlot>(nameof(EquipmentSlot.PerformEquipmentAction))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            VariableDefinition equipmentQualityTiersVar = il.AddVariable<QualityTier[]>();
            VariableDefinition equipmentQualityIndexVar = il.AddVariable<int>();

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<EquipmentSlot, QualityTier[]>>(getRandomEquipmentQualityTiers);
            c.Emit(OpCodes.Stloc, equipmentQualityTiersVar);

            static QualityTier[] getRandomEquipmentQualityTiers(EquipmentSlot equipmentSlot)
            {
                CharacterBody body = equipmentSlot ? equipmentSlot.characterBody : null;
                Inventory inventory = body ? body.inventory : null;

                QualityTier[] equipmentQualityTiers = Array.Empty<QualityTier>();

                ItemQualityCounts randomEquipmentTrigger = ItemQualitiesContent.ItemQualityGroups.RandomEquipmentTrigger.GetItemCountsEffective(inventory);
                if (randomEquipmentTrigger.TotalQualityCount > 0)
                {
                    Span<QualityTier> equipmentQualityTiersSpan = stackalloc QualityTier[randomEquipmentTrigger.TotalCount];

                    int equipmentQualityTierIndex = 0;
                    for (QualityTier qualityTier = QualityTier.None; qualityTier < QualityTier.Count; qualityTier++)
                    {
                        int tierCount = randomEquipmentTrigger[qualityTier];
                        if (tierCount > 0)
                        {
                            equipmentQualityTiersSpan.Slice(equipmentQualityTierIndex, tierCount).Fill(qualityTier);
                            equipmentQualityTierIndex += tierCount;
                        }
                    }

                    equipmentQualityTiers = equipmentQualityTiersSpan.ToArray();
                }

                return equipmentQualityTiers;
            }

            c.Emit(OpCodes.Ldc_I4_0);
            c.Emit(OpCodes.Stloc, equipmentQualityIndexVar);

            c.Goto(foundCursors[1].Next, MoveType.Before); // call EquipmentSlot.PerformEquipmentAction

            c.Emit(OpCodes.Ldloc, equipmentQualityTiersVar);
            c.Emit(OpCodes.Ldloca, equipmentQualityIndexVar);
            c.EmitDelegate<TryUpgradeEquipmentQualityDelegate>(tryUpgradeEquipmentQuality);

            static EquipmentDef tryUpgradeEquipmentQuality(EquipmentDef equipmentDef, QualityTier[] qualityTiers, ref int qualityTierIndex)
            {
                EquipmentIndex equipmentIndex = equipmentDef ? equipmentDef.equipmentIndex : EquipmentIndex.None;

                if (equipmentIndex != EquipmentIndex.None && qualityTiers.Length > 0)
                {
                    QualityTier qualityTier = qualityTiers[qualityTierIndex % qualityTiers.Length];
                    qualityTierIndex++;

                    EquipmentIndex qualityEquipmentIndex = QualityCatalog.GetEquipmentIndexOfQuality(equipmentIndex, qualityTier);
                    if (qualityEquipmentIndex != EquipmentIndex.None && qualityEquipmentIndex != equipmentIndex)
                    {
                        equipmentDef = EquipmentCatalog.GetEquipmentDef(qualityEquipmentIndex);
                        equipmentIndex = qualityEquipmentIndex;
                    }
                }

                return equipmentDef;
            }
        }

        delegate EquipmentDef TryUpgradeEquipmentQualityDelegate(EquipmentDef equipmentDef, QualityTier[] qualityTiers, ref int qualityTierIndex);
    }
}
