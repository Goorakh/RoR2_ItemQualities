using ItemQualities.Utilities.Extensions;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using System;

namespace ItemQualities.Equipments
{
    internal static class TeamWarCry
    {
        [SystemInitializer]
        private static void Init()
        {
            IL.RoR2.EquipmentSlot.FireTeamWarCry += EquipmentSlot_FireTeamWarCry;
        }

        private static void EquipmentSlot_FireTeamWarCry(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            int patchCount = 0;
            while (c.TryGotoNext(MoveType.After,
                                 x => x.MatchLdsfld(typeof(RoR2Content.Buffs), nameof(RoR2Content.Buffs.TeamWarCry))))
            {
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate<Func<BuffDef, EquipmentSlot, BuffDef>>(getBuff);

                static BuffDef getBuff(BuffDef buffDef, EquipmentSlot equipmentSlot)
                {
                    QualityTier qualityTier = equipmentSlot.GetCurrentEquipmentActionQualityTier();
                    if (qualityTier > QualityTier.None)
                    {
                        BuffIndex buffIndex = buffDef ? buffDef.buffIndex : BuffIndex.None;

                        BuffIndex qualityBuffIndex = QualityCatalog.GetBuffIndexOfQuality(buffIndex, qualityTier);
                        if (qualityBuffIndex != BuffIndex.None && qualityBuffIndex != buffIndex)
                        {
                            buffDef = BuffCatalog.GetBuffDef(qualityBuffIndex);
                            buffIndex = qualityBuffIndex;
                        }
                    }

                    return buffDef;
                }

                patchCount++;
            }

            if (patchCount == 0)
            {
                Log.Warning("Failed to find patch location");
            }
            else
            {
                Log.Debug($"Found {patchCount} patch location(s)");
            }
        }
    }
}
