using ItemQualities.Utilities.Extensions;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using R2API;
using RoR2;
using System;

namespace ItemQualities.Items
{
    internal static class EquipmentMagazineVoid
    {
        [SystemInitializer]
        private static void Init()
        {
            RecalculateStatsAPI.GetStatCoefficients += getStatCoefficients;

            IL.RoR2.CharacterBody.HandleConstructTurret += CharacterBody_HandleConstructTurret;
        }

        private static void CharacterBody_HandleConstructTurret(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (!c.TryGotoNext(MoveType.AfterLabel,
                               x => x.MatchCallOrCallvirt<MasterSummon>(nameof(MasterSummon.Perform))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Emit(OpCodes.Dup);
            c.EmitDelegate<Action<MasterSummon>>(handleTurretSummon);

            static void handleTurretSummon(MasterSummon turretSummon)
            {
                if (turretSummon?.summonerBodyObject &&
                    turretSummon.summonerBodyObject.TryGetComponent(out CharacterBody summonerBody) &&
                    summonerBody.inventory)
                {
                    ItemQualityCounts equipmentMagazineVoid = summonerBody.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.EquipmentMagazineVoid);
                    if (equipmentMagazineVoid.TotalQualityCount > 0)
                    {
                        turretSummon.preSpawnSetupCallback += onTurretSpawned;
                        void onTurretSpawned(CharacterMaster turretMaster)
                        {
                            if (turretMaster && turretMaster.inventory)
                            {
                                turretMaster.inventory.GiveItemsPermanent(ItemQualitiesContent.ItemQualityGroups.BoostDamageVoid, equipmentMagazineVoid);
                            }
                        }
                    }
                }
            }
        }

        public static void ModifyTakeDamage(ref float damageValue, HealthComponent victim, DamageInfo damageInfo)
        {
            if (damageInfo == null)
                return;

            CharacterBody attackerBody = damageInfo.attacker ? damageInfo.attacker.GetComponent<CharacterBody>() : null;
            Inventory attackerInventory = attackerBody ? attackerBody.inventory : null;
            if (!attackerInventory)
                return;

            ItemQualityCounts equipmentMagazineVoid = new ItemQualityCounts();

            if ((damageInfo.damageType.damageSource & DamageSource.Special) != 0)
            {
                equipmentMagazineVoid += attackerInventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.EquipmentMagazineVoid);
            }

            equipmentMagazineVoid += attackerInventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.BoostDamageVoid);

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
                    damageInfo.damageType.AddModdedDamageType(DamageTypes.Void);
                }
            }
        }

        private static void getStatCoefficients(CharacterBody sender, RecalculateStatsAPI.StatHookEventArgs args)
        {
            if (!sender || !sender.inventory)
                return;

            ItemQualityCounts equipmentMagazineVoid = sender.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.EquipmentMagazineVoid);
            if (equipmentMagazineVoid.TotalQualityCount > 0)
            {
                float specialSkillCooldownScale;
                switch (equipmentMagazineVoid.HighestQuality)
                {
                    case QualityTier.Uncommon:
                        specialSkillCooldownScale = 1f - 0.1f;
                        break;
                    case QualityTier.Rare:
                        specialSkillCooldownScale = 1f - 0.2f;
                        break;
                    case QualityTier.Epic:
                        specialSkillCooldownScale = 1f - 0.4f;
                        break;
                    case QualityTier.Legendary:
                        specialSkillCooldownScale = 1f - 0.55f;
                        break;
                    default:
                        specialSkillCooldownScale = 1f;
                        Log.Error($"Quality tier {equipmentMagazineVoid.HighestQuality} is not implemented");
                        break;
                }

                args.specialSkill.cooldownMultiplier *= specialSkillCooldownScale;
            }
        }
    }
}
