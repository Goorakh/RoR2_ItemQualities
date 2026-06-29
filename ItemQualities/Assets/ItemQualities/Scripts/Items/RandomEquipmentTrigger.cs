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
            IL.RoR2.EquipmentSlot.OnEquipmentExecuted_byte_byte_EquipmentIndex += EquipmentSlot_OnEquipmentExecuted;
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
            {
                // Setup tiers array
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate<Func<EquipmentSlot, QualityTier[]>>(getRandomEquipmentQualityTiers);
                c.Emit(OpCodes.Stloc, equipmentQualityTiersVar);

                static QualityTier[] getRandomEquipmentQualityTiers(EquipmentSlot equipmentSlot)
                {
                    CharacterBody body = equipmentSlot ? equipmentSlot.characterBody : null;
                    Inventory inventory = body ? body.inventory : null;

                    QualityTier[] equipmentQualityTiers = Array.Empty<QualityTier>();
                    if (inventory)
                    {
                        ItemQualityCounts randomEquipmentTrigger = inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.RandomEquipmentTrigger);
                        if (randomEquipmentTrigger.TotalQualityCount > 0)
                        {
                            Span<QualityTier> equipmentQualityTiersSpan = stackalloc QualityTier[randomEquipmentTrigger.TotalQualityCount];

                            int equipmentQualityTierIndex = 0;
                            for (QualityTier qualityTier = 0; qualityTier < QualityTier.Count; qualityTier++)
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
                    }

                    Log.Debug($"{equipmentSlot} equipment quality tiers: [{string.Join(", ", equipmentQualityTiers)}]");

                    return equipmentQualityTiers;
                }
            }

            VariableDefinition equipmentQualityIndexVar = il.AddVariable<int>();
            {
                // setup tier index
                c.Emit(OpCodes.Ldc_I4_0);
                c.Emit(OpCodes.Stloc, equipmentQualityIndexVar);
            }

            c.Goto(foundCursors[1].Next, MoveType.Before); // call EquipmentSlot.PerformEquipmentAction

            c.Emit(OpCodes.Ldloc, equipmentQualityTiersVar);
            c.Emit(OpCodes.Ldloc, equipmentQualityIndexVar);
            c.EmitDelegate<Func<EquipmentDef, QualityTier[], int, EquipmentDef>>(tryUpgradeEquipmentQuality);

            static EquipmentDef tryUpgradeEquipmentQuality(EquipmentDef equipmentDef, QualityTier[] qualityTiers, int qualityTierIndex)
            {
                EquipmentIndex equipmentIndex = equipmentDef ? equipmentDef.equipmentIndex : EquipmentIndex.None;

                if (equipmentIndex != EquipmentIndex.None && qualityTierIndex < qualityTiers.Length)
                {
                    QualityTier qualityTier = qualityTiers[qualityTierIndex];
                    if (qualityTier != QualityTier.None)
                    {
                        EquipmentIndex qualityEquipmentIndex = QualityCatalog.GetEquipmentIndexOfQuality(equipmentIndex, qualityTier);
                        if (qualityEquipmentIndex != EquipmentIndex.None && qualityEquipmentIndex != equipmentIndex)
                        {
                            equipmentDef = EquipmentCatalog.GetEquipmentDef(qualityEquipmentIndex);
                            equipmentIndex = qualityEquipmentIndex;

                            // Quality equipment blacklisted: pass null to the rest of the code, equipment activation will fail and this equipment will be skipped
                            if (!equipmentDef.canBeRandomlyTriggered)
                            {
                                Log.Debug($"Quality equipment {equipmentDef.name} is not valid for random trigger, skipping");

                                equipmentDef = null;
                                equipmentIndex = EquipmentIndex.None;
                            }
                        }
                        else
                        {
                            // If quality does not exist for this equipment, skip it
                            equipmentDef = null;
                            equipmentIndex = EquipmentIndex.None;
                        }
                    }
                }

                Log.Debug($"Attempting equipment: {equipmentDef}");

                return equipmentDef;
            }

            c.Goto(foundCursors[1].Next, MoveType.After); // call EquipmentSlot.PerformEquipmentAction

            c.Emit(OpCodes.Dup);
            c.Emit(OpCodes.Ldloca, equipmentQualityIndexVar);
            c.EmitDelegate<OnEquipmentActionPerformedDelegate>(onEquipmentActionPerformed);

            static void onEquipmentActionPerformed(bool success, ref int qualityTierIndex)
            {
                // Only increment the quality tier sequence if the equipment could activate
                // In other words: Keep trying equipments at the current quality until one is found, *then* move on to the next quality tier
                if (success)
                {
                    qualityTierIndex++;
                }
            }
        }

        delegate void OnEquipmentActionPerformedDelegate(bool success, ref int qualityTierIndex);
    }
}
