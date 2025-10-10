using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using System;

namespace ItemQualities.Items
{
    static class EnergizedOnEquipmentUse
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
                               x => x.MatchLdsfld(typeof(RoR2Content.Buffs), nameof(RoR2Content.Buffs.Energized))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Goto(foundCursors[0].Next, MoveType.After);

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<BuffDef, EquipmentSlot, BuffDef>>(getWarhornBuff);

            static BuffDef getWarhornBuff(BuffDef warhornBuffDef, EquipmentSlot equipmentSlot)
            {
                CharacterBody body = equipmentSlot ? equipmentSlot.characterBody : null;
                Inventory inventory = body ? body.inventory : null;

                BuffIndex warhornBuffIndex = warhornBuffDef ? warhornBuffDef.buffIndex : BuffIndex.None;
                if (warhornBuffIndex != BuffIndex.None)
                {
                    QualityTier buffQuality = ItemQualitiesContent.ItemQualityGroups.EnergizedOnEquipmentUse.GetHighestQualityInInventory(inventory);
                    BuffIndex qualityWarhornBuffIndex = QualityCatalog.GetBuffIndexOfQuality(warhornBuffIndex, buffQuality);

                    if (qualityWarhornBuffIndex != BuffIndex.None && qualityWarhornBuffIndex != warhornBuffIndex)
                    {
                        warhornBuffDef = BuffCatalog.GetBuffDef(qualityWarhornBuffIndex);
                        warhornBuffIndex = qualityWarhornBuffIndex;
                    }
                }

                return warhornBuffDef;
            }
        }
    }
}
