using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using RoR2.Orbs;
using RoR2BepInExPack.Utilities;
using System;

namespace ItemQualities.Equipments
{
    internal static class Lightning
    {
        private static readonly FixedConditionalWeakTable<LightningStrikeOrb, LightningOrbQualityInfo> _lightningOrbQualityInfoLookup = new FixedConditionalWeakTable<LightningStrikeOrb, LightningOrbQualityInfo>();

        private sealed class LightningOrbQualityInfo
        {
            public QualityTier QualityTier = QualityTier.None;

            public int BouncesRemaining;

            public void CopyTo(LightningOrbQualityInfo other)
            {
                other.QualityTier = QualityTier;
                other.BouncesRemaining = BouncesRemaining;
            }
        }

        [SystemInitializer]
        private static void Init()
        {
            IL.RoR2.EquipmentSlot.FireLightning += EquipmentSlot_FireLightning;
            On.RoR2.Orbs.LightningStrikeOrb.OnArrival += LightningStrikeOrb_OnArrival;
        }

        private static void EquipmentSlot_FireLightning(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (!c.TryGotoNext(MoveType.After,
                               x => x.MatchNewobj<LightningStrikeOrb>()))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Emit(OpCodes.Dup);
            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Action<LightningStrikeOrb, EquipmentSlot>>(setOrbQualityTier);

            static void setOrbQualityTier(LightningStrikeOrb lightningStrikeOrb, EquipmentSlot equipmentSlot)
            {
                if (lightningStrikeOrb == null || !equipmentSlot)
                    return;

                QualityTier qualityTier = equipmentSlot.GetCurrentEquipmentActionQualityTier();
                if (qualityTier == QualityTier.None)
                {
                    _lightningOrbQualityInfoLookup.Remove(lightningStrikeOrb);
                    return;
                }

                float bounceChance;
                switch (qualityTier)
                {
                    case QualityTier.Uncommon:
                        bounceChance = 50f;
                        break;
                    case QualityTier.Rare:
                        bounceChance = 120f;
                        break;
                    case QualityTier.Epic:
                        bounceChance = 220f;
                        break;
                    case QualityTier.Legendary:
                        bounceChance = 400f;
                        break;
                    default:
                        bounceChance = 0f;
                        Log.Warning($"Quality tier {qualityTier} is not implemented");
                        break;
                }

                CharacterMaster master = equipmentSlot.characterBody ? equipmentSlot.characterBody.master : null;

                LightningOrbQualityInfo qualityInfo = _lightningOrbQualityInfoLookup.GetOrCreateValue(lightningStrikeOrb);
                qualityInfo.QualityTier = qualityTier;
                qualityInfo.BouncesRemaining = RollUtil.GetOverflowRoll(bounceChance, master, false);
            }
        }

        private static void LightningStrikeOrb_OnArrival(On.RoR2.Orbs.LightningStrikeOrb.orig_OnArrival orig, LightningStrikeOrb self)
        {
            orig(self);

            if (_lightningOrbQualityInfoLookup.TryGetValue(self, out LightningOrbQualityInfo qualityInfo))
            {
                if (qualityInfo.BouncesRemaining > 0 && self.target)
                {
                    qualityInfo.BouncesRemaining--;

                    OrbManager.instance.AddOrb(self);
                }
                else
                {
                    _lightningOrbQualityInfoLookup.Remove(self);
                }
            }
        }
    }
}
