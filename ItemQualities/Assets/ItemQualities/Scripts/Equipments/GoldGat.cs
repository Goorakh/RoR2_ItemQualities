using HG;
using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using RoR2BepInExPack.GameAssetPathsBetter;
using System;
using UnityEngine;

namespace ItemQualities.Equipments
{
    static class GoldGat
    {
        [SystemInitializer]
        static void Init()
        {
            AddressableUtil.LoadAssetAsync<GameObject>(RoR2_Base_GoldGat.GoldGatController_prefab).OnSuccess(goldGatAttachmentPrefab =>
            {
                goldGatAttachmentPrefab.EnsureComponent<QualityTierContext>();
            });

            On.RoR2.EquipmentSlot.UpdateGoldGat += EquipmentSlot_UpdateGoldGat;
            IL.EntityStates.GoldGat.GoldGatFire.FireBullet += GoldGatFire_FireBullet;
        }

        static void EquipmentSlot_UpdateGoldGat(On.RoR2.EquipmentSlot.orig_UpdateGoldGat orig, EquipmentSlot self)
        {
            orig(self);

            if (self && self.goldgatControllerObject && self.goldgatControllerObject.TryGetComponentCached(out QualityTierContext qualityContext))
            {
                qualityContext.QualityTier = self.GetActiveEquipmentQualityTier();
            }
        }

        static void GoldGatFire_FireBullet(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            VariableDefinition maxFireFrequencyMultiplierVar = il.AddVariable<float>();

            int patchCount = 0;
            while (c.TryGotoNext(MoveType.After,
                                 x => x.MatchLdsfld<EntityStates.GoldGat.GoldGatFire>(nameof(EntityStates.GoldGat.GoldGatFire.maxFireFrequency))))
            {
                c.Emit(OpCodes.Ldloc, maxFireFrequencyMultiplierVar);
                c.Emit(OpCodes.Mul);

                patchCount++;
            }

            if (patchCount == 0)
            {
                Log.Error("Failed to find maxFireFrequency patch location");
                return;
            }
            else
            {
                Log.Debug($"Found {patchCount} maxFireFrequency patch location(s)");
            }

            c.Goto(0);

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<EntityStates.GoldGat.GoldGatFire, float>>(getMaxFireFrequencyMultiplier);
            c.Emit(OpCodes.Stloc, maxFireFrequencyMultiplierVar);

            static float getMaxFireFrequencyMultiplier(EntityStates.GoldGat.GoldGatFire self)
            {
                QualityTier qualityTier = QualityTierContext.GetQualityTier(self.gameObject);
                switch (qualityTier)
                {
                    case QualityTier.None:
                        return 1.0f;
                    case QualityTier.Uncommon:
                        return 1.2f;
                    case QualityTier.Rare:
                        return 1.4f;
                    case QualityTier.Epic:
                        return 1.8f;
                    case QualityTier.Legendary:
                        return 2.5f;
                    default:
                        Log.Warning($"Quality tier {qualityTier} is not implemented");
                        return 1.0f;
                }
            }
        }
    }
}
