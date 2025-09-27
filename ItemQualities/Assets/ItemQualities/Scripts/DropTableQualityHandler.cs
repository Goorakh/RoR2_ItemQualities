using HG;
using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ItemQualities
{
    static class DropTableQualityHandler
    {
        static readonly WeightedSelection<QualityTier> _tierSelection = new WeightedSelection<QualityTier>();

        static bool _allowQualityGeneration = true;

        static DropTableQualityHandler()
        {
            _tierSelection.AddChoice(QualityTier.Uncommon, 0.7f);
            _tierSelection.AddChoice(QualityTier.Rare, 0.20f);
            _tierSelection.AddChoice(QualityTier.Epic, 0.08f);
            _tierSelection.AddChoice(QualityTier.Legendary, 0.02f);
        }

        [SystemInitializer]
        static void Init()
        {
            On.RoR2.PickupDropTable.GenerateDrop += PickupDropTable_GenerateDrop;
            On.RoR2.PickupDropTable.GenerateUniqueDrops += PickupDropTable_GenerateUniqueDrops;

            On.RoR2.ShopTerminalBehavior.GenerateNewPickupServer_bool += On_ShopTerminalBehavior_GenerateNewPickupServer_bool;

            On.RoR2.Items.RandomlyLunarUtils.CheckForLunarReplacement += RandomlyLunarUtils_CheckForLunarReplacement;
            On.RoR2.Items.RandomlyLunarUtils.CheckForLunarReplacementUniqueArray += RandomlyLunarUtils_CheckForLunarReplacementUniqueArray;

            // All the things that are too old to use a droptable...
            IL.RoR2.ChestBehavior.PickFromList += ChestBehavior_PickFromList;
            IL.EntityStates.ScavMonster.FindItem.OnEnter += FindItem_OnEnter;
            IL.RoR2.Inventory.GiveRandomItems += Inventory_GiveRandomItems;
            IL.RoR2.MultiShopController.CreateTerminals += MultiShopController_CreateTerminals;
            IL.RoR2.ScavBackpackBehavior.PickFromList += ScavBackpackBehavior_PickFromList;
            IL.RoR2.ShopTerminalBehavior.GenerateNewPickupServer_bool += IL_ShopTerminalBehavior_GenerateNewPickupServer_bool;
            IL.RoR2.ShrineChanceBehavior.AddShrineStack += ShrineChanceBehavior_AddShrineStack;
            IL.RoR2.BossGroup.DropRewards += BossGroup_DropRewards;
            IL.RoR2.Inventory.GiveRandomEquipment += Inventory_GiveRandomEquipment;
            IL.RoR2.Inventory.GiveRandomEquipment_Xoroshiro128Plus += Inventory_GiveRandomEquipment;
        }

        static void On_ShopTerminalBehavior_GenerateNewPickupServer_bool(On.RoR2.ShopTerminalBehavior.orig_GenerateNewPickupServer_bool orig, ShopTerminalBehavior self, bool newHidden)
        {
            try
            {
                if (self.name.Contains("Duplicator", StringComparison.OrdinalIgnoreCase))
                {
                    _allowQualityGeneration = false;
                }

                orig(self, newHidden);
            }
            finally
            {
                _allowQualityGeneration = true;
            }
        }

        static QualityTier rollQuality(Xoroshiro128Plus rng)
        {
            return _tierSelection.Evaluate(rng.nextNormalizedFloat);
        }

        static PickupIndex tryUpgradeQuality(PickupIndex pickupIndex, Xoroshiro128Plus rng)
        {
            if (!_allowQualityGeneration || pickupIndex == PickupIndex.none)
                return pickupIndex;

            if (rng.nextNormalizedFloat > 0.05f)
                return pickupIndex;
            
            QualityTier qualityTier = rollQuality(rng);
            PickupIndex qualityPickupIndex = QualityCatalog.GetPickupIndexOfQuality(pickupIndex, qualityTier);

            if (qualityPickupIndex != PickupIndex.none)
            {
                if (qualityPickupIndex == pickupIndex)
                {
                    Log.Warning($"Pickup {pickupIndex} is missing quality variant {qualityTier}");
                }
                else
                {
                    Log.Debug($"Upgraded tier of {pickupIndex}: {qualityPickupIndex}");
                }

                pickupIndex = qualityPickupIndex;
            }

            return pickupIndex;
        }

        static PickupIndex PickupDropTable_GenerateDrop(On.RoR2.PickupDropTable.orig_GenerateDrop orig, PickupDropTable self, Xoroshiro128Plus rng)
        {
            PickupIndex dropPickupIndex = orig(self, rng);

            dropPickupIndex = tryUpgradeQuality(dropPickupIndex, rng);

            return dropPickupIndex;
        }

        static PickupIndex[] PickupDropTable_GenerateUniqueDrops(On.RoR2.PickupDropTable.orig_GenerateUniqueDrops orig, PickupDropTable self, int maxDrops, Xoroshiro128Plus rng)
        {
            PickupIndex[] dropPickupIncides = orig(self, maxDrops, rng);

            for (int i = 0; i < dropPickupIncides.Length; i++)
            {
                dropPickupIncides[i] = tryUpgradeQuality(dropPickupIncides[i], rng);
            }

            return dropPickupIncides;
        }

        static PickupIndex RandomlyLunarUtils_CheckForLunarReplacement(On.RoR2.Items.RandomlyLunarUtils.orig_CheckForLunarReplacement orig, PickupIndex pickupIndex, Xoroshiro128Plus rng)
        {
            QualityTier qualityTier = QualityCatalog.GetQualityTier(pickupIndex);

            return QualityCatalog.GetPickupIndexOfQuality(orig(pickupIndex, rng), qualityTier);
        }

        static void RandomlyLunarUtils_CheckForLunarReplacementUniqueArray(On.RoR2.Items.RandomlyLunarUtils.orig_CheckForLunarReplacementUniqueArray orig, PickupIndex[] pickupIndices, Xoroshiro128Plus rng)
        {
            QualityTier[] qualityTiers = new QualityTier[pickupIndices.Length];

            for (int i = 0; i < pickupIndices.Length; i++)
            {
                qualityTiers[i] = QualityCatalog.GetQualityTier(pickupIndices[i]);
            }

            orig(pickupIndices, rng);

            for (int i = 0; i < pickupIndices.Length; i++)
            {
                QualityTier qualityTier = ArrayUtils.GetSafe(qualityTiers, i, QualityTier.None);
                pickupIndices[i] = QualityCatalog.GetPickupIndexOfQuality(pickupIndices[i], qualityTier);
            }
        }

        static void ChestBehavior_PickFromList(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            int patchCount = 0;

            while (c.TryGotoNext(MoveType.Before,
                                 x => x.MatchCallOrCallvirt<ChestBehavior>("set_" + nameof(ChestBehavior.dropPickup))))
            {
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate<Func<PickupIndex, ChestBehavior, PickupIndex>>(pickQuality);

                static PickupIndex pickQuality(PickupIndex originalPickupIndex, ChestBehavior chestBehavior)
                {
                    return tryUpgradeQuality(originalPickupIndex, chestBehavior.rng);
                }

                patchCount++;
                c.SearchTarget = SearchTarget.Next;
            }

            if (patchCount == 0)
            {
                Log.Error("Failed to find patch location");
            }
            else
            {
                Log.Debug($"Found {patchCount} patch location(s)");
            }
        }

        static void FindItem_OnEnter(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (!c.TryGotoNext(MoveType.Before,
                               x => x.MatchStfld<EntityStates.ScavMonster.FindItem>(nameof(EntityStates.ScavMonster.FindItem.dropPickup))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<PickupIndex, EntityStates.ScavMonster.FindItem, PickupIndex>>(pickQuality);

            static PickupIndex pickQuality(PickupIndex originalPickupIndex, EntityStates.ScavMonster.FindItem findItem)
            {
                PickupIndex qualityPickupIndex = tryUpgradeQuality(originalPickupIndex, RoR2Application.rng);
                if (qualityPickupIndex != originalPickupIndex && findItem.PickupIsNonBlacklistedItem(qualityPickupIndex))
                {
                    return qualityPickupIndex;
                }
                else
                {
                    return originalPickupIndex;
                }
            }
        }

        static void Inventory_GiveRandomItems(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (!c.TryGotoNext(MoveType.Before,
                               x => x.MatchCallOrCallvirt(typeof(PickupCatalog), nameof(PickupCatalog.GetPickupDef))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.EmitDelegate<Func<PickupIndex, PickupIndex>>(pickQuality);

            static PickupIndex pickQuality(PickupIndex originalPickupIndex)
            {
                return tryUpgradeQuality(originalPickupIndex, RoR2Application.rng);
            }
        }

        static void MultiShopController_CreateTerminals(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (!c.TryGotoNext(MoveType.Before,
                               x => x.MatchCallOrCallvirt<ShopTerminalBehavior>(nameof(ShopTerminalBehavior.SetPickupIndex))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            VariableDefinition isHiddenTempVar = il.AddVariable<bool>();

            c.Emit(OpCodes.Stloc, isHiddenTempVar);

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<PickupIndex, MultiShopController, PickupIndex>>(pickQuality);

            static PickupIndex pickQuality(PickupIndex originalPickupIndex, MultiShopController multiShopController)
            {
                return tryUpgradeQuality(originalPickupIndex, multiShopController.rng);
            }

            c.Emit(OpCodes.Ldloc, isHiddenTempVar);
        }

        static void ScavBackpackBehavior_PickFromList(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            int patchCount = 0;

            while (c.TryGotoNext(MoveType.Before,
                                 x => x.MatchStfld<ScavBackpackBehavior>(nameof(ScavBackpackBehavior.dropPickup))))
            {
                c.EmitDelegate<Func<PickupIndex, PickupIndex>>(pickQuality);

                static PickupIndex pickQuality(PickupIndex originalPickupIndex)
                {
                    return tryUpgradeQuality(originalPickupIndex, RoR2Application.rng);
                }

                patchCount++;
                c.SearchTarget = SearchTarget.Next;
            }

            if (patchCount == 0)
            {
                Log.Error("Failed to find patch location");
            }
            else
            {
                Log.Debug($"Found {patchCount} patch location(s)");
            }
        }

        static void IL_ShopTerminalBehavior_GenerateNewPickupServer_bool(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (!c.TryGotoNext(MoveType.After,
                               x => x.MatchCallOrCallvirt(out MethodReference method) && method?.Name?.StartsWith("<GenerateNewPickupServer>g__Pick") == true))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<PickupIndex, ShopTerminalBehavior, PickupIndex>>(pickQuality);

            static PickupIndex pickQuality(PickupIndex originalPickupIndex, ShopTerminalBehavior shopTerminalBehavior)
            {
                return tryUpgradeQuality(originalPickupIndex, shopTerminalBehavior.rng);
            }
        }

        static void ShrineChanceBehavior_AddShrineStack(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            MethodInfo pickupIndexSelectionEvaluate = typeof(WeightedSelection<PickupIndex>).GetMethod(nameof(WeightedSelection<PickupIndex>.Evaluate));

            if (!c.TryGotoNext(MoveType.After,
                               x => x.MatchCallOrCallvirt(pickupIndexSelectionEvaluate)))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<PickupIndex, ShrineChanceBehavior, PickupIndex>>(pickQuality);

            static PickupIndex pickQuality(PickupIndex originalPickupIndex, ShrineChanceBehavior shrineChanceBehavior)
            {
                return tryUpgradeQuality(originalPickupIndex, shrineChanceBehavior.rng);
            }
        }

        static void BossGroup_DropRewards(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            MethodInfo nextElementUniformPickupIndexList = typeof(Xoroshiro128Plus).GetMethods().FirstOrDefault(m => m.Name == nameof(Xoroshiro128Plus.NextElementUniform) && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType.IsGenericType && m.GetParameters()[0].ParameterType.GetGenericTypeDefinition() == typeof(List<>))?.MakeGenericMethod(typeof(PickupIndex));
            if (nextElementUniformPickupIndexList == null)
            {
                Log.Error("Failed to find method Xoroshiro128Plus.NextElementUniform<T>(List<T>)");
                return;
            }

            if (!c.TryGotoNext(MoveType.After,
                               x => x.MatchCallOrCallvirt(nextElementUniformPickupIndexList)))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<PickupIndex, BossGroup, PickupIndex>>(pickQuality);

            static PickupIndex pickQuality(PickupIndex originalPickupIndex, BossGroup bossGroup)
            {
                return tryUpgradeQuality(originalPickupIndex, bossGroup.rng);
            }
        }

        static void Inventory_GiveRandomEquipment(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (!il.Method.TryFindParameter<Xoroshiro128Plus>(out ParameterDefinition rngParameter))
                rngParameter = null;

            if (!c.TryGotoNext(MoveType.Before,
                               x => x.MatchCallOrCallvirt<Inventory>(nameof(Inventory.SetEquipmentIndex))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            if (rngParameter != null)
            {
                c.Emit(OpCodes.Ldarg, rngParameter);
            }
            else
            {
                c.Emit(OpCodes.Ldnull);
            }

            c.EmitDelegate<Func<EquipmentIndex, Xoroshiro128Plus, EquipmentIndex>>(pickQuality);

            static EquipmentIndex pickQuality(EquipmentIndex originalEquipmentIndex, Xoroshiro128Plus rng)
            {
                PickupIndex originalPickupIndex = PickupCatalog.FindPickupIndex(originalEquipmentIndex);

                PickupIndex qualityPickupIndex = tryUpgradeQuality(originalPickupIndex, rng ?? RoR2Application.rng);
                PickupDef qualityPickupDef = PickupCatalog.GetPickupDef(qualityPickupIndex);
                EquipmentIndex qualityEquipmentIndex = qualityPickupDef != null ? qualityPickupDef.equipmentIndex : EquipmentIndex.None;

                return qualityEquipmentIndex != EquipmentIndex.None ? qualityEquipmentIndex : originalEquipmentIndex;
            }
        }
    }
}
