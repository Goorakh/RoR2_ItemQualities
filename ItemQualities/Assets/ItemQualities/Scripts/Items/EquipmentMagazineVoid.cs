using ItemQualities.Utilities.Extensions;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using System;

namespace ItemQualities.Items
{
    static class EquipmentMagazineVoid
    {
        [SystemInitializer]
        static void Init()
        {
            ItemHooks.TakeDamageModifier += modifyTakeDamage;

            IL.RoR2.CharacterBody.RecalculateStats += CharacterBody_RecalculateStats;
        }

        static void modifyTakeDamage(ref float damageValue, DamageInfo damageInfo)
        {
            if (damageInfo == null || (damageInfo.damageType.damageSource & DamageSource.Special) == 0)
                return;

            CharacterBody attackerBody = damageInfo.attacker ? damageInfo.attacker.GetComponent<CharacterBody>() : null;
            Inventory attackerInventory = attackerBody ? attackerBody.inventory : null;
            if (!attackerInventory)
                return;

            ItemQualityCounts equipmentMagazineVoid = ItemQualitiesContent.ItemQualityGroups.EquipmentMagazineVoid.GetItemCountsEffective(attackerInventory);
            if (equipmentMagazineVoid.TotalQualityCount > 0)
            {
                float damageIncrease = (0.1f * equipmentMagazineVoid.UncommonCount) +
                                       (0.2f * equipmentMagazineVoid.RareCount) +
                                       (0.4f * equipmentMagazineVoid.EpicCount) +
                                       (0.5f * equipmentMagazineVoid.LegendaryCount);

                if (damageIncrease > 0f)
                {
                    damageValue *= 1f + damageIncrease;
                    damageInfo.damageColorIndex = DamageColorIndex.Void;
                }
            }
        }

        static void CharacterBody_RecalculateStats(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (!ItemHooks.TryFindNextItemCountVariable(c, typeof(DLC1Content.Items), nameof(DLC1Content.Items.EquipmentMagazineVoid), out VariableDefinition equipmentMagazineVoidItemCountVar))
            {
                Log.Error("Failed to find itemCount variable");
                return;
            }

            if (!c.TryFindNext(out ILCursor[] foundCursors,
                               x => x.MatchCallOrCallvirt<SkillLocator>("get_" + nameof(SkillLocator.specialBonusStockSkill)),
                               x => x.MatchLdloc(equipmentMagazineVoidItemCountVar.Index),
                               x => x.MatchMul()))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Goto(foundCursors[2].Next, MoveType.Before); // mul

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<float, CharacterBody, float>>(getCooldownScale);

            static float getCooldownScale(float cooldownScale, CharacterBody body)
            {
                Inventory inventory = body ? body.inventory : null;
                QualityTier qualityTier = ItemQualitiesContent.ItemQualityGroups.EquipmentMagazineVoid.GetItemCountsEffective(inventory).HighestQuality;
                switch (qualityTier)
                {
                    case QualityTier.Uncommon:
                        cooldownScale *= 1f - 0.1f;
                        break;
                    case QualityTier.Rare:
                        cooldownScale *= 1f - 0.2f;
                        break;
                    case QualityTier.Epic:
                        cooldownScale *= 1f - 0.4f;
                        break;
                    case QualityTier.Legendary:
                        cooldownScale *= 1f - 0.55f;
                        break;
                }

                return cooldownScale;
            }
        }
    }
}
