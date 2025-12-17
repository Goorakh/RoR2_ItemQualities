using Mono.Cecil.Cil;
using MonoMod.Cil;
using R2API;
using RoR2;
using System;

namespace ItemQualities.Items
{
    static class Icicle
    {
        [SystemInitializer]
        static void Init()
        {
            IL.RoR2.IcicleAuraController.FixedUpdate += IcicleAuraController_FixedUpdate;
            On.RoR2.IcicleAuraController.UpdateRadius += IcicleAuraController_UpdateRadius;
        }

        static void IcicleAuraController_FixedUpdate(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (!c.TryGotoNext(MoveType.Before,
                               x => x.MatchStfld<BlastAttack>(nameof(BlastAttack.damageType))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<DamageTypeCombo, IcicleAuraController, DamageTypeCombo>>(getIcicleDamageType);

            static DamageTypeCombo getIcicleDamageType(DamageTypeCombo damageType, IcicleAuraController icicleAuraController)
            {
                CharacterBody ownerBody = icicleAuraController ? icicleAuraController.cachedOwnerInfo.characterBody : null;
                if (ownerBody)
                {
                    Inventory ownerInventory = ownerBody ? ownerBody.inventory : null;

                    ItemQualityCounts icicle = ItemQualitiesContent.ItemQualityGroups.Icicle.GetItemCountsEffective(ownerInventory);
                    if (icicle.TotalQualityCount > 0)
                    {
                        float frostChance = (20f * icicle.UncommonCount) +
                                            (30f * icicle.RareCount) +
                                            (50f * icicle.EpicCount) +
                                            (75f * icicle.LegendaryCount);

                        if (Util.CheckRoll(frostChance, ownerBody.master))
                        {
                            damageType.AddModdedDamageType(DamageTypes.Frost6s);
                        }
                    }
                }

                return damageType;
            }
        }

        static void IcicleAuraController_UpdateRadius(On.RoR2.IcicleAuraController.orig_UpdateRadius orig, IcicleAuraController self)
        {
            orig(self);

            CharacterBody ownerBody = self ? self.cachedOwnerInfo.characterBody : null;
            Inventory ownerInventory = ownerBody ? ownerBody.inventory : null;

            ItemQualityCounts icicle = ItemQualitiesContent.ItemQualityGroups.Icicle.GetItemCountsEffective(ownerInventory);
            if (icicle.TotalQualityCount > 0)
            {
                float radiusIncrease = (0.30f * icicle.UncommonCount) +
                                       (0.50f * icicle.RareCount) +
                                       (0.80f * icicle.EpicCount) +
                                       (1.00f * icicle.LegendaryCount);

                if (radiusIncrease > 0)
                {
                    self.actualRadius *= 1f + radiusIncrease;
                }
            }
        }
    }
}
