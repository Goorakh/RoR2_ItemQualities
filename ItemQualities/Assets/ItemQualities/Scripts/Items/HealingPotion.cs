using ItemQualities.Orbs;
using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using R2API;
using RoR2;
using RoR2.Orbs;
using RoR2BepInExPack.Utilities;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ItemQualities.Items
{
    internal static class HealingPotion
    {
        private static readonly FixedConditionalWeakTable<DotController, BurnEffectController> _chemicalBurnEffectControllers = new FixedConditionalWeakTable<DotController, BurnEffectController>();

        private static readonly BurnEffectController.EffectParams _chemicalBurnEffectParams = new BurnEffectController.EffectParams();

        public static DotController.DotIndex ChemicalBurnDotIndex { get; private set; } = DotController.DotIndex.None;

        public const float ChemicalBurnDuration = 8f;

        public static ModdedProcType HealingPotionProcType { get; private set; } = ModdedProcType.Invalid;

        private static ItemIndex[] _consumedItemIndices = Array.Empty<ItemIndex>();
        private static EquipmentIndex[] _consumedEquipmentIndices = Array.Empty<EquipmentIndex>();

        [SystemInitializer(typeof(ItemCatalog), typeof(EquipmentCatalog))]
        private static void InitConsumedPickups()
        {
            List<ItemIndex> consumedItemIndices = new List<ItemIndex>();
            List<EquipmentIndex> consumedEquipmentIndices = new List<EquipmentIndex>();

            for (ItemIndex itemIndex = 0; (int)itemIndex < ItemCatalog.itemCount; itemIndex++)
            {
                if (ItemCatalog.GetItemDef(itemIndex).isConsumed)
                {
                    consumedItemIndices.Add(itemIndex);
                }
            }

            for (EquipmentIndex equipmentIndex = 0; (int)equipmentIndex < EquipmentCatalog.equipmentCount; equipmentIndex++)
            {
                if (EquipmentCatalog.GetEquipmentDef(equipmentIndex).isConsumed)
                {
                    consumedEquipmentIndices.Add(equipmentIndex);
                }
            }

            _consumedItemIndices = consumedItemIndices.ToArray();
            _consumedEquipmentIndices = consumedEquipmentIndices.ToArray();
        }

        [SystemInitializer]
        private static void Init()
        {
            IL.RoR2.HealthComponent.UpdateLastHitTime += HealthComponent_UpdateLastHitTime;

            GlobalEventManager.onServerDamageDealt += onServerDamageDealt;

            HealingPotionProcType = ProcTypeAPI.ReserveProcType();

            const float damageCoefficientPerSecond = 1f;
            const float interval = 0.25f;

            DotController.DotDef chemicalBurnDotDef = new DotController.DotDef
            {
                interval = interval,
                damageCoefficient = damageCoefficientPerSecond * interval,
                damageColorIndex = DamageColorIndex.Bleed,
                associatedBuff = ItemQualitiesContent.Buffs.ChemicalBurn,
                resetTimerOnAdd = true,
            };

            ChemicalBurnDotIndex = DotAPI.RegisterDotDef(chemicalBurnDotDef, customDotVisual: chemicalBurnDotVisual);

            _chemicalBurnEffectParams.overlayMaterial = ItemQualitiesContent.Materials.ChemicalGooOverlay;
            _chemicalBurnEffectParams.fireEffectPrefab = ItemQualitiesContent.Prefabs.ChemicalBurnEffect;
        }

        private static void chemicalBurnDotVisual(DotController dotController)
        {
            BurnEffectController chemicalBurnEffectController = _chemicalBurnEffectControllers.GetValueOrDefault(dotController, null);

            bool hasChemicalBurnEffect = chemicalBurnEffectController;
            bool shouldHaveChemicalBurnEffect = dotController.HasDotActive(ChemicalBurnDotIndex);

            if (hasChemicalBurnEffect != shouldHaveChemicalBurnEffect)
            {
                if (shouldHaveChemicalBurnEffect)
                {
                    CharacterBody victimBody = dotController.victimBody;
                    ModelLocator victimModelLocator = victimBody ? victimBody.modelLocator : null;
                    Transform victimModelTransform = victimModelLocator ? victimModelLocator.modelTransform : null;
                    if (victimModelTransform)
                    {
                        chemicalBurnEffectController = dotController.gameObject.AddComponent<BurnEffectController>();
                        chemicalBurnEffectController.effectType = _chemicalBurnEffectParams;
                        chemicalBurnEffectController.target = victimModelTransform.gameObject;

                        _chemicalBurnEffectControllers[dotController] = chemicalBurnEffectController;
                    }
                }
                else
                {
                    chemicalBurnEffectController.HandleDestroy();
                    _chemicalBurnEffectControllers.Remove(dotController);
                }
            }
        }

        private static void onServerDamageDealt(DamageReport damageReport)
        {
            if (damageReport.damageInfo.procChainMask.HasModdedProc(HealingPotionProcType) || damageReport.damageInfo.procCoefficient <= 0f)
            {
                return;
            }

            if (!damageReport.victimBody || !damageReport.attackerBody || !damageReport.attackerBody.inventory)
            {
                return;
            }

            ItemQualityCounts healingPotion = damageReport.attackerBody.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.HealingPotion);
            if (healingPotion.TotalQualityCount == 0)
            {
                return;
            }

            int consumedItemCount = 0;
            foreach (ItemIndex consumedItemIndex in _consumedItemIndices)
            {
                consumedItemCount += damageReport.attackerBody.inventory.CalculateEffectiveItemStacks(consumedItemIndex);
            }

            foreach (EquipmentIndex consumedEquipmentIndex in _consumedEquipmentIndices)
            {
                if (damageReport.attackerBody.inventory.HasEquipment(consumedEquipmentIndex))
                {
                    consumedItemCount++;
                }
            }

            float extraChancePerConsumedItem = healingPotion.HighestQuality switch
            {
                QualityTier.Uncommon => 0.5f,
                QualityTier.Rare => 1f,
                QualityTier.Epic => 3f,
                QualityTier.Legendary => 5f,
                _ => 0f
            };

            float damageCoefficient = (healingPotion.UncommonCount * (2.5f / ChemicalBurnDuration)) +
                                      (healingPotion.RareCount * (6f / ChemicalBurnDuration)) +
                                      (healingPotion.EpicCount * (10f / ChemicalBurnDuration)) +
                                      (healingPotion.LegendaryCount * (16f / ChemicalBurnDuration));

            float procChance = 5f + (extraChancePerConsumedItem * consumedItemCount);

            if (RollUtil.CheckRoll(procChance, damageReport.attackerMaster, damageReport.damageInfo.procChainMask.HasProc(ProcType.SureProc)))
            {
                ProcChainMask procChainMask = damageReport.damageInfo.procChainMask;
                procChainMask.AddModdedProc(HealingPotionProcType);

                Orb orb = new HealingPotionOrb
                {
                    origin = damageReport.attackerBody.corePosition,
                    target = damageReport.victimBody.mainHurtBox,
                    attacker = damageReport.attacker,
                    teamIndex = damageReport.attackerTeamIndex,
                    isCrit = damageReport.damageInfo.crit,
                    procChainMask = procChainMask,
                    procCoefficient = 0.5f,
                    damageColorIndex = DamageColorIndex.Bleed,
                    damageType = DamageType.Generic,
                    radius = 15f,
                    damageValue = damageCoefficient * damageReport.attackerBody.damage,
                    dotDamageMultiplier = damageCoefficient,
                };

                OrbManager.instance.AddOrb(orb);
            }
        }

        private static void HealthComponent_UpdateLastHitTime(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            VariableDefinition potionItemTransformationVar = null;
            if (!c.TryGotoNext(MoveType.After,
                               x => x.MatchLdfld<HealthComponent.ItemCounts>(nameof(HealthComponent.ItemCounts.healingPotion))) ||
                !c.TryGotoNext(MoveType.Before,
                               x => x.MatchLdloca(typeof(Inventory.ItemTransformation), il, out potionItemTransformationVar),
                               x => x.MatchLdarg(0),
                               x => x.MatchLdfld<HealthComponent>(nameof(HealthComponent.body)),
                               x => x.MatchCallOrCallvirt<CharacterBody>("get_" + nameof(CharacterBody.inventory)),
                               x => x.MatchLdloca(typeof(Inventory.ItemTransformation.TryTransformResult), il, out _),
                               x => x.MatchCallOrCallvirt<Inventory.ItemTransformation>(nameof(Inventory.ItemTransformation.TryTransform)),
                               x => x.MatchBrfalse(out _)))
            {
                Log.PatchError(il, "Failed to find patch location");
                return;
            }

            c.Emit(OpCodes.Ldarg_0);
            c.Emit(OpCodes.Ldloca, potionItemTransformationVar);
            c.EmitDelegate<TryConsumeQualityElixirsDelegate>(tryConsumeQualityElixirs);

            static void tryConsumeQualityElixirs(HealthComponent healthComponent, ref Inventory.ItemTransformation itemTransformation)
            {
                CharacterBody body = healthComponent ? healthComponent.body : null;
                Inventory inventory = body ? body.inventory : null;
                if (!inventory)
                    return;

                ItemQualityCounts elixir = inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.HealingPotion);

                if (elixir.BaseItemCount == 0 && elixir.TotalQualityCount > 0)
                {
                    for (QualityTier qualityTier = 0; qualityTier < QualityTier.Count; qualityTier++)
                    {
                        if (elixir[qualityTier] > 0)
                        {
                            itemTransformation.originalItemIndex = ItemQualitiesContent.ItemQualityGroups.HealingPotion.GetItemIndex(qualityTier);
                            itemTransformation.newItemIndex = ItemQualitiesContent.ItemQualityGroups.HealingPotionConsumed.GetItemIndex(qualityTier);
                            break;
                        }
                    }
                }
            }
        }

        private delegate void TryConsumeQualityElixirsDelegate(HealthComponent healthComponent, ref Inventory.ItemTransformation itemTransformation);
    }
}
