using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace ItemQualities
{
    static class DropTableQualityHandler
    {
        static readonly WeightedSelection<QualityTier> _tierSelection = new WeightedSelection<QualityTier>();

        static bool _allowQualityGeneration = true;

        static CharacterMaster _currentDropGenerationOwnerMaster = null;
        static TeamIndex _currentDropGenerationTeamAffiliation = TeamIndex.None;

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
            On.RoR2.PickupDropTable.GeneratePickup += PickupDropTable_GeneratePickup;
            On.RoR2.PickupDropTable.GenerateDistinctPickups += PickupDropTable_GenerateDistinctPickups;

            On.RoR2.ShopTerminalBehavior.GenerateNewPickupServer_bool += On_ShopTerminalBehavior_GenerateNewPickupServer_bool;
            On.RoR2.ArenaMissionController.AddItemStack += ArenaMissionController_AddItemStack;
            On.RoR2.Artifacts.MonsterTeamGainsItemsArtifactManager.GrantMonsterTeamItem += MonsterTeamGainsItemsArtifactManager_GrantMonsterTeamItem;
            On.RoR2.InfiniteTowerRun.AdvanceWave += InfiniteTowerRun_AdvanceWave;
            On.RoR2.ScavengerItemGranter.Start += ScavengerItemGranter_Start;

            // All the things that are too old to use a droptable...
            IL.RoR2.ChestBehavior.PickFromList += ChestBehavior_PickFromList;
            IL.EntityStates.ScavMonster.FindItem.OnEnter += FindItem_OnEnter;
            IL.RoR2.Inventory.GiveRandomItems_int_bool_bool += Inventory_GiveRandomItems;
            IL.RoR2.Inventory.GiveRandomItems_int_ItemTierArray += Inventory_GiveRandomItems;
            IL.RoR2.MultiShopController.CreateTerminals += MultiShopController_CreateTerminals;
            IL.RoR2.ScavBackpackBehavior.PickFromList += ScavBackpackBehavior_PickFromList;
            IL.RoR2.ShopTerminalBehavior.GenerateNewPickupServer_bool += IL_ShopTerminalBehavior_GenerateNewPickupServer_bool;
            IL.RoR2.ShrineChanceBehavior.AddShrineStack += ShrineChanceBehavior_AddShrineStack;
            IL.RoR2.BossGroup.DropRewards += BossGroup_DropRewards;
            IL.RoR2.Inventory.GiveRandomEquipment += Inventory_GiveRandomEquipment;
            IL.RoR2.Inventory.GiveRandomEquipment_Xoroshiro128Plus += Inventory_GiveRandomEquipment;
            IL.RoR2.MasterDropDroplet.DropItems += MasterDropDroplet_DropItems;
        }

        public static PickupRollInfo GetCurrentPickupRollInfo(CharacterMaster rollOwnerMaster = null)
        {
            if (!rollOwnerMaster)
            {
                rollOwnerMaster = _currentDropGenerationOwnerMaster;
            }

            TeamIndex teamAffiliation = _currentDropGenerationTeamAffiliation;
            if (teamAffiliation == TeamIndex.None)
            {
                teamAffiliation = TeamIndex.Player;
            }

            if (rollOwnerMaster)
            {
                teamAffiliation = rollOwnerMaster.teamIndex;
            }

            return new PickupRollInfo(rollOwnerMaster, teamAffiliation);
        }

        static bool pickupCheckNotAIBlacklist(PickupIndex pickupIndex)
        {
            PickupDef pickupDef = PickupCatalog.GetPickupDef(pickupIndex);
            ItemDef itemDef = ItemCatalog.GetItemDef(pickupDef != null ? pickupDef.itemIndex : ItemIndex.None);

            return itemDef && itemDef.DoesNotContainTag(ItemTag.AIBlacklist);
        }

        static QualityTier rollQuality(Xoroshiro128Plus rng)
        {
            return _tierSelection.Evaluate(rng.nextNormalizedFloat);
        }

        static PickupIndex tryUpgradeQuality(PickupIndex pickupIndex, Xoroshiro128Plus rng, CharacterMaster master = null, Func<PickupIndex, bool> isPickupAllowedFunc = null)
        {
            rng = new Xoroshiro128Plus(rng.nextUlong);

            if (!_allowQualityGeneration || pickupIndex == PickupIndex.none)
                return pickupIndex;

            PickupRollInfo rollInfo = GetCurrentPickupRollInfo(master);

            if (!rollInfo.IsPlayerAffiliation)
            {
                isPickupAllowedFunc ??= pickupCheckNotAIBlacklist;
            }

            if (Configs.Debug.LogItemQualities)
            {
                Log.Debug($"Rolling quality for pickup {pickupIndex}, luck={rollInfo.Luck}, master={master}, teamAffiliation={rollInfo.TeamAffiliation}");
            }

            PickupIndex qualityPickupIndex = pickupIndex;
            QualityTier currentPickupQualityTier = QualityCatalog.GetQualityTier(qualityPickupIndex);

            if (rng.nextNormalizedFloat <= 1f - Mathf.Pow(1f - (Configs.General.GlobalQualityChance.Value / 100f), 1 + rollInfo.Luck))
            {
                for (int i = rollInfo.Luck; i >= 0; i--)
                {
                    QualityTier qualityTier = rollQuality(rng);
                    PickupIndex qualityPickupIndexCandidate = QualityCatalog.GetPickupIndexOfQuality(qualityPickupIndex, qualityTier);
                    if (qualityTier > currentPickupQualityTier && (isPickupAllowedFunc == null || isPickupAllowedFunc(qualityPickupIndexCandidate)))
                    {
                        qualityPickupIndex = qualityPickupIndexCandidate;
                        currentPickupQualityTier = qualityTier;
                    }
                }
            }

            if (Configs.Debug.LogItemQualities && qualityPickupIndex != pickupIndex)
            {
                Log.Debug($"Upgraded tier of {pickupIndex}: {qualityPickupIndex}");
            }

            return qualityPickupIndex;
        }

        static void On_ShopTerminalBehavior_GenerateNewPickupServer_bool(On.RoR2.ShopTerminalBehavior.orig_GenerateNewPickupServer_bool orig, ShopTerminalBehavior self, bool newHidden)
        {
            try
            {
                bool isItemCost = false;
                if (self.TryGetComponent(out PurchaseInteraction purchaseInteraction))
                {
                    switch (purchaseInteraction.costType)
                    {
                        case CostTypeIndex.WhiteItem:
                        case CostTypeIndex.GreenItem:
                        case CostTypeIndex.RedItem:
                        case CostTypeIndex.Equipment:
                        case CostTypeIndex.VolatileBattery:
                        case CostTypeIndex.LunarItemOrEquipment:
                        case CostTypeIndex.BossItem:
                        case CostTypeIndex.ArtifactShellKillerItem:
                        case CostTypeIndex.TreasureCacheItem:
                        case CostTypeIndex.TreasureCacheVoidItem:
                            isItemCost = true;
                            break;
                        default:
                            isItemCost |= CustomCostTypeIndex.IsQualityItemCostType(purchaseInteraction.costType);
                            break;
                    }
                }

                _allowQualityGeneration = !isItemCost;

                orig(self, newHidden);
            }
            finally
            {
                _allowQualityGeneration = true;
            }
        }

        static void ArenaMissionController_AddItemStack(On.RoR2.ArenaMissionController.orig_AddItemStack orig, ArenaMissionController self)
        {
            try
            {
                _currentDropGenerationTeamAffiliation = TeamIndex.Monster;
                orig(self);
            }
            finally
            {
                _currentDropGenerationTeamAffiliation = TeamIndex.None;
            }
        }

        static void MonsterTeamGainsItemsArtifactManager_GrantMonsterTeamItem(On.RoR2.Artifacts.MonsterTeamGainsItemsArtifactManager.orig_GrantMonsterTeamItem orig)
        {
            try
            {
                _currentDropGenerationTeamAffiliation = TeamIndex.Monster;
                orig();
            }
            finally
            {
                _currentDropGenerationTeamAffiliation = TeamIndex.None;
            }
        }

        static void InfiniteTowerRun_AdvanceWave(On.RoR2.InfiniteTowerRun.orig_AdvanceWave orig, InfiniteTowerRun self)
        {
            try
            {
                _currentDropGenerationTeamAffiliation = TeamIndex.Monster;
                orig(self);
            }
            finally
            {
                _currentDropGenerationTeamAffiliation = TeamIndex.None;
            }
        }

        static void ScavengerItemGranter_Start(On.RoR2.ScavengerItemGranter.orig_Start orig, ScavengerItemGranter self)
        {
            try
            {
                _currentDropGenerationOwnerMaster = self ? self.GetComponent<CharacterMaster>() : null;
                orig(self);
            }
            finally
            {
                _currentDropGenerationOwnerMaster = null;
            }
        }

        static Func<PickupIndex, bool> getDropTableFilterFunc(PickupDropTable pickupDropTable)
        {
            if (!pickupDropTable)
                return null;

            ItemTag[] requiredItemTags = Array.Empty<ItemTag>();
            ItemTag[] bannedItemTags = Array.Empty<ItemTag>();

            foreach (FieldInfo field in pickupDropTable.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (field.IsSerialized() && field.FieldType == typeof(ItemTag[]))
                {
                    if (field.Name.Contains("required", StringComparison.OrdinalIgnoreCase))
                    {
                        requiredItemTags = (field.GetValue(pickupDropTable) as ItemTag[]) ?? Array.Empty<ItemTag>();
                    }
                    else if (field.Name.Contains("banned", StringComparison.OrdinalIgnoreCase) ||
                             field.Name.Contains("forbidden", StringComparison.OrdinalIgnoreCase))
                    {
                        bannedItemTags = (field.GetValue(pickupDropTable) as ItemTag[]) ?? Array.Empty<ItemTag>();
                    }
                }
            }

            if (requiredItemTags.Length == 0 && bannedItemTags.Length == 0)
                return null;

            bool pickupPassesFilter(PickupIndex pickupIndex)
            {
                PickupDef pickupDef = PickupCatalog.GetPickupDef(pickupIndex);
                ItemDef itemDef = ItemCatalog.GetItemDef(pickupDef != null ? pickupDef.itemIndex : ItemIndex.None);

                if (requiredItemTags.Length > 0 || bannedItemTags.Length > 0)
                {
                    if (!itemDef)
                        return false;

                    foreach (ItemTag requiredItemTag in requiredItemTags)
                    {
                        if (!itemDef.ContainsTag(requiredItemTag))
                            return false;
                    }

                    foreach (ItemTag bannedItemTag in bannedItemTags)
                    {
                        if (itemDef.ContainsTag(bannedItemTag))
                            return false;
                    }
                }

                return true;
            }

            return pickupPassesFilter;
        }
        
        static UniquePickup PickupDropTable_GeneratePickup(On.RoR2.PickupDropTable.orig_GeneratePickup orig, PickupDropTable self, Xoroshiro128Plus rng)
        {
            UniquePickup dropPickupIndex = orig(self, rng);

            if (self is not QualityPickupDropTable)
            {
                dropPickupIndex = dropPickupIndex.WithPickupIndex(tryUpgradeQuality(dropPickupIndex.pickupIndex, rng, null, getDropTableFilterFunc(self)));
            }

            return dropPickupIndex;
        }

        static void PickupDropTable_GenerateDistinctPickups(On.RoR2.PickupDropTable.orig_GenerateDistinctPickups orig, PickupDropTable self, List<UniquePickup> dest, int desiredCount, Xoroshiro128Plus rng, bool allowLoop)
        {
            orig(self, dest, desiredCount, rng, allowLoop);

            if (self is not QualityPickupDropTable)
            {
                Func<PickupIndex, bool> isPickupAllowedFunc = getDropTableFilterFunc(self);

                for (int i = 0; i < Math.Min(desiredCount, dest.Count); i++)
                {
                    dest[i] = dest[i].WithPickupIndex(tryUpgradeQuality(dest[i].pickupIndex, rng, null, isPickupAllowedFunc));
                }
            }
        }

        static void ChestBehavior_PickFromList(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            int patchCount = 0;

            while (c.TryGotoNext(MoveType.Before,
                                 x => x.MatchCallOrCallvirt<ChestBehavior>("set_" + nameof(ChestBehavior.currentPickup))))
            {
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate<Func<UniquePickup, ChestBehavior, UniquePickup>>(pickQuality);

                static UniquePickup pickQuality(UniquePickup originalPickup, ChestBehavior chestBehavior)
                {
                    if (originalPickup.isValid)
                    {
                        return originalPickup.WithPickupIndex(tryUpgradeQuality(originalPickup.pickupIndex, chestBehavior.rng));
                    }

                    return originalPickup;
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
                CharacterBody body = findItem.characterBody;
                CharacterMaster master = body ? body.master : null;

                return tryUpgradeQuality(originalPickupIndex, RoR2Application.rng, master, findItem.PickupIsNonBlacklistedItem);
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

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<PickupIndex, Inventory, PickupIndex>>(pickQuality);

            static PickupIndex pickQuality(PickupIndex originalPickupIndex, Inventory inventory)
            {
                return tryUpgradeQuality(originalPickupIndex, RoR2Application.rng, inventory ? inventory.GetComponent<CharacterMaster>() : null);
            }
        }

        static void MultiShopController_CreateTerminals(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (!c.TryGotoNext(MoveType.Before,
                               x => x.MatchCallOrCallvirt<ShopTerminalBehavior>(nameof(ShopTerminalBehavior.SetPickup))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            VariableDefinition isHiddenTempVar = il.AddVariable<bool>();

            c.Emit(OpCodes.Stloc, isHiddenTempVar);

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<UniquePickup, MultiShopController, UniquePickup>>(pickQuality);

            static UniquePickup pickQuality(UniquePickup originalPickup, MultiShopController multiShopController)
            {
                if (originalPickup.isValid)
                {
                    return originalPickup.WithPickupIndex(tryUpgradeQuality(originalPickup.pickupIndex, multiShopController.rng));
                }

                return originalPickup;
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

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<EquipmentIndex, Xoroshiro128Plus, Inventory, EquipmentIndex>>(pickQuality);

            static EquipmentIndex pickQuality(EquipmentIndex originalEquipmentIndex, Xoroshiro128Plus rng, Inventory inventory)
            {
                PickupIndex originalPickupIndex = PickupCatalog.FindPickupIndex(originalEquipmentIndex);

                PickupIndex qualityPickupIndex = tryUpgradeQuality(originalPickupIndex, rng ?? RoR2Application.rng, inventory ? inventory.GetComponent<CharacterMaster>() : null);
                PickupDef qualityPickupDef = PickupCatalog.GetPickupDef(qualityPickupIndex);
                EquipmentIndex qualityEquipmentIndex = qualityPickupDef != null ? qualityPickupDef.equipmentIndex : EquipmentIndex.None;

                return qualityEquipmentIndex != EquipmentIndex.None ? qualityEquipmentIndex : originalEquipmentIndex;
            }
        }

        static void MasterDropDroplet_DropItems(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (!c.TryFindNext(out ILCursor[] foundCursors,
                               x => x.MatchLdfld<MasterDropDroplet>(nameof(MasterDropDroplet.pickupsToDrop)),
                               x => x.MatchCallOrCallvirt(typeof(PickupCatalog), nameof(PickupCatalog.FindPickupIndex))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Goto(foundCursors[1].Next, MoveType.After);

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<PickupIndex, MasterDropDroplet, PickupIndex>>(pickQuality);

            static PickupIndex pickQuality(PickupIndex originalPickupIndex, MasterDropDroplet masterDropDroplet)
            {
                return tryUpgradeQuality(originalPickupIndex, masterDropDroplet.rng ?? RoR2Application.rng, masterDropDroplet.GetComponent<CharacterMaster>());
            }
        }
    }
}
