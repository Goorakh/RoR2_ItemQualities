using ItemQualities.Utilities.Extensions;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using System;

namespace ItemQualities.Equipments
{
    static class LifestealOnHit
    {
        [SystemInitializer]
        static void Init()
        {
            IL.RoR2.EquipmentSlot.FireLifeStealOnHit += EquipmentSlot_FireLifeStealOnHit;
        }

        static void EquipmentSlot_FireLifeStealOnHit(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (!c.TryGotoNext(MoveType.After,
                               x => x.MatchLdsfld(typeof(RoR2Content.Buffs), nameof(RoR2Content.Buffs.LifeSteal))))
            {
                Log.Error("Failed to find buff patch location");
                return;
            }

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<BuffDef, EquipmentSlot, BuffDef>>(getBuffDef);

            static BuffDef getBuffDef(BuffDef buffDef, EquipmentSlot equipmentSlot)
            {
                BuffIndex buffIndex = buffDef ? buffDef.buffIndex : BuffIndex.None;

                QualityTier qualityTier = equipmentSlot.GetCurrentEquipmentActionQualityTier();
                if (qualityTier > QualityTier.None)
                {
                    BuffIndex qualityBuffIndex = QualityCatalog.GetBuffIndexOfQuality(buffIndex, qualityTier);
                    if (qualityBuffIndex != BuffIndex.None && qualityBuffIndex != buffIndex)
                    {
                        buffDef = BuffCatalog.GetBuffDef(qualityBuffIndex);
                        buffIndex = qualityBuffIndex;
                    }
                }

                return buffDef;
            }

            if (!c.TryGotoNext(MoveType.After,
                               x => x.MatchLdcR4(out _)))
            {
                Log.Error("Failed to find duration patch location");
                return;
            }

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<float, EquipmentSlot, float>>(getBuffDuration);

            static float getBuffDuration(float duration, EquipmentSlot equipmentSlot)
            {
                QualityTier qualityTier = equipmentSlot.GetCurrentEquipmentActionQualityTier();
                switch (qualityTier)
                {
                    case QualityTier.None:
                        break;
                    case QualityTier.Uncommon:
                        duration += 2f;
                        break;
                    case QualityTier.Rare:
                        duration += 4f;
                        break;
                    case QualityTier.Epic:
                        duration += 8f;
                        break;
                    case QualityTier.Legendary:
                        duration += 17f;
                        break;
                    default:
                        Log.Warning($"Quality tier {qualityTier} is not implemented");
                        break;
                }

                return duration;
            }
        }
    }
}
