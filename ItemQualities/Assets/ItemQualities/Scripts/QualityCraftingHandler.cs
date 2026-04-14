using HG;
using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using R2API.Utils;
using RoR2;
using RoR2.ContentManagement;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;

namespace ItemQualities
{
    static class QualityCraftingHandler
    {
        static readonly HashSet<CraftableDef> _qualityCraftableDefs = new HashSet<CraftableDef>();

        [InitDuringStartupPhase(GameInitPhase.PreFrame)]
        static void Init()
        {
            SystemInitializerInjector.InjectDependency(typeof(CraftableCatalog), typeof(QualityCatalog));

            On.RoR2.CraftableCatalog.Init += CraftableCatalog_Init;

            IL.RoR2.CraftableCatalog.SetCraftableDefs += CraftableCatalog_SetCraftableDefs;
        }

        static void CraftableCatalog_Init(On.RoR2.CraftableCatalog.orig_Init orig)
        {
            appendQualityCraftableDefs(ref ContentManager._craftableDefs);
            orig();
        }

        static void appendQualityCraftableDefs(ref CraftableDef[] allCraftableDefs)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            if (_qualityCraftableDefs.Count > 0)
            {
                foreach (CraftableDef qualityCraftableDef in _qualityCraftableDefs)
                {
                    CraftableDef.Destroy(qualityCraftableDef);
                }

                _qualityCraftableDefs.Clear();
            }

            // Would be nice to just append these to the content pack directly,
            // but we need to use the RecipeIngredient.Validate method to resolve indices, which depends on some catalogs,
            // so we need to do this after PickupCatalog and IngredientTypeCatalog are initialized.

            List<CraftableDef> qualityCraftableDefs = new List<CraftableDef>(allCraftableDefs.Length * (int)QualityTier.Count);
            int totalRecipeCount = 0;

            Span<List<Recipe>> qualityRecipesByResultQuality = new List<Recipe>[(int)QualityTier.Count + 1];
            foreach (ref List<Recipe> recipes in qualityRecipesByResultQuality)
            {
                recipes = new List<Recipe>();
            }

            foreach (CraftableDef craftableDef in allCraftableDefs)
            {
                if (!craftableDef)
                    continue;

                PickupDef resultPickup = craftableDef.GetPickupDefFromResult();
                PickupIndex resultPickupIndex = resultPickup != null ? resultPickup.pickupIndex : PickupIndex.none;
                if (!resultPickupIndex.isValid)
                    continue;

                foreach (ref readonly List<Recipe> recipes in qualityRecipesByResultQuality)
                {
                    recipes.Clear();
                }

                HashSet<PickupIndex[]> allRecipeCombinations = new HashSet<PickupIndex[]>(UnorderedCollectionComparer<PickupIndex>.Default);

                foreach (Recipe recipe in craftableDef.recipes)
                {
                    if (recipe?.ingredients == null)
                        continue;

                    int ingredientsCount = recipe.ingredients.Length;
                    if (ingredientsCount == 0)
                        continue;

                    // Contains all (including quality) pickup indices of ingredients that can be used for each slot in this recipe
                    Span<PickupIndex[]> possibleIngredientsBySlot = new PickupIndex[ingredientsCount][];

                    bool allSlotsHaveIngredients = true;

                    for (int i = 0; i < ingredientsCount; i++)
                    {
                        RecipeIngredient ingredient = recipe.ingredients[i];

                        // A little hacky, but since the CraftableCatalog is not initialized at this point, we need to do some of the lifting ourselves
                        if (ingredient.IsDefinedPickup() && ingredient.pickupIndex == PickupIndex.none)
                        {
                            if (ingredient.pickup is ItemDef itemDef)
                            {
                                ingredient.pickupIndex = PickupCatalog.FindPickupIndex(itemDef.itemIndex);
                            }
                            else if (ingredient.pickup is EquipmentDef equipmentDef)
                            {
                                ingredient.pickupIndex = PickupCatalog.FindPickupIndex(equipmentDef.equipmentIndex);
                            }
                        }

                        using var _ = SetPool<PickupIndex>.RentCollection(out HashSet<PickupIndex> possibleIngredients);

                        foreach (PickupIndex ingredientPickupIndex in PickupCatalog.allPickupIndices)
                        {
                            if (ingredient.Validate(QualityCatalog.GetPickupIndexOfQuality(ingredientPickupIndex, QualityTier.None)))
                            {
                                possibleIngredients.Add(ingredientPickupIndex);
                            }
                        }

                        if (possibleIngredients.Count > 0)
                        {
                            possibleIngredientsBySlot[i] = possibleIngredients.ToArray();
                        }
                        else
                        {
                            allSlotsHaveIngredients = false;
                            break;
                        }
                    }

                    if (!allSlotsHaveIngredients)
                        continue;

                    allRecipeCombinations.Clear();

                    Span<int> ingredientIndices = stackalloc int[ingredientsCount];
                    bool hasRecordedAllRecipeCombinations;
                    do
                    {
                        PickupIndex[] recipeIngredients = new PickupIndex[ingredientsCount];
                        for (int slot = 0; slot < ingredientsCount; slot++)
                        {
                            int ingredientIndex = ingredientIndices[slot];
                            recipeIngredients[slot] = possibleIngredientsBySlot[slot][ingredientIndex];
                        }

                        allRecipeCombinations.Add(recipeIngredients);

                        bool incrementedIngredientIndex = false;
                        for (int slot = 0; slot < ingredientsCount; slot++)
                        {
                            ref int ingredientIndex = ref ingredientIndices[slot];

                            if (ingredientIndex < possibleIngredientsBySlot[slot].Length - 1)
                            {
                                ingredientIndex++;
                                incrementedIngredientIndex = true;
                                break;
                            }
                            else
                            {
                                ingredientIndex = 0;
                            }
                        }

                        hasRecordedAllRecipeCombinations = !incrementedIngredientIndex;
                    } while (!hasRecordedAllRecipeCombinations);

                    foreach (PickupIndex[] ingredients in allRecipeCombinations)
                    {
                        int averageIngredientQualityValue = 0;
                        int numQualityIngredients = 0;

                        foreach (PickupIndex ingredientPickupIndex in ingredients)
                        {
                            QualityTier qualityTier = QualityCatalog.GetQualityTier(ingredientPickupIndex);
                            if (qualityTier != QualityTier.None)
                            {
                                averageIngredientQualityValue += (int)qualityTier;
                                numQualityIngredients++;
                            }
                        }

                        // result should be:
                        // when all ingredients quality:
                        // * average quality rounded down
                        // when not all ingredients quality (but at least 1 is):
                        // * common result
                        // when no ingredients quality:
                        // * ignore

                        if (numQualityIngredients == 0)
                            continue;

                        bool allIngredientsQuality = numQualityIngredients == ingredients.Length;
                        QualityTier averageIngredientQualityTier = (QualityTier)(averageIngredientQualityValue / numQualityIngredients);

                        QualityTier resultQualityTier = allIngredientsQuality ? averageIngredientQualityTier : QualityTier.None;

                        // If the result quality does not exist, fall back to base item result, no matter the ingredients
                        if (QualityCatalog.GetPickupIndexOfQuality(resultPickupIndex, resultQualityTier) == resultPickupIndex)
                        {
                            resultQualityTier = QualityTier.None;
                        }

                        Recipe qualityRecipe = new Recipe
                        {
                            amountToDrop = recipe.amountToDrop,
                            priority = recipe.priority,
                            ingredients = new RecipeIngredient[ingredients.Length],
                        };

                        for (int i = 0; i < ingredients.Length; i++)
                        {
                            qualityRecipe.ingredients[i] = new RecipeIngredient
                            {
                                pickup = getPickupDefObject(ingredients[i]),
                                type = IngredientTypeIndex.AssetReference,
                                forbiddenTags = Array.Empty<ItemTag>(),
                                requiredTags = Array.Empty<ItemTag>(),
                            };
                        }

                        qualityRecipesByResultQuality[(int)resultQualityTier + 1].Add(qualityRecipe);
                    }
                }

                for (QualityTier qualityTier = QualityTier.None; qualityTier < QualityTier.Count; qualityTier++)
                {
                    ref readonly List<Recipe> qualityRecipes = ref qualityRecipesByResultQuality[(int)qualityTier + 1];
                    if (qualityRecipes.Count > 0)
                    {
                        PickupIndex qualityResultPickupIndex = QualityCatalog.GetPickupIndexOfQuality(resultPickupIndex, qualityTier);
                        if (qualityTier == QualityTier.None || resultPickupIndex != qualityResultPickupIndex)
                        {
                            UnityEngine.Object qualityResultPickupDefObject = getPickupDefObject(qualityResultPickupIndex);
                            if (qualityResultPickupDefObject)
                            {
                                CraftableDef qualityCraftableDef = ScriptableObject.CreateInstance<CraftableDef>();
                                qualityCraftableDef.name = $"{craftableDef.name}{qualityTier}";
                                qualityCraftableDef.pickup = qualityResultPickupDefObject;
                                qualityCraftableDef.recipes = qualityRecipes.ToArray();

                                qualityCraftableDefs.Add(qualityCraftableDef);

                                totalRecipeCount += qualityCraftableDef.recipes.Length;
                            }
                        }
                    }
                }
            }

            if (qualityCraftableDefs.Count > 0)
            {
                _qualityCraftableDefs.UnionWith(qualityCraftableDefs);

                int baseCraftableDefsCount = allCraftableDefs.Length;
                Array.Resize(ref allCraftableDefs, baseCraftableDefsCount + qualityCraftableDefs.Count);
                qualityCraftableDefs.CopyTo(allCraftableDefs, baseCraftableDefsCount);
            }

            _qualityCraftableDefs.TrimExcess();

            Log.Debug($"Added {qualityCraftableDefs.Count} quality CraftableDef(s) (total {totalRecipeCount} recipe(s)) ({stopwatch.Elapsed.TotalMilliseconds:F0}ms)");

#if DEBUG
            // Quality craft recipes logging

            var sb = new System.Text.StringBuilder();

            foreach (CraftableDef craftableDef in _qualityCraftableDefs)
            {
                List<PickupIndex[]> allValidIngredientCombinations = new List<PickupIndex[]>();

                foreach (Recipe recipe in craftableDef.recipes)
                {
                    PickupIndex[] combination = new PickupIndex[recipe.ingredients.Length];
                    for (int i = 0; i < recipe.ingredients.Length; i++)
                    {
                        RecipeIngredient ingredient = recipe.ingredients[i];

                        PickupIndex ingredientPickupIndex = PickupIndex.none;
                        if (ingredient.pickup is ItemDef itemDef)
                        {
                            ingredientPickupIndex = PickupCatalog.FindPickupIndex(itemDef.itemIndex);
                        }
                        else if (ingredient.pickup is EquipmentDef equipmentDef)
                        {
                            ingredientPickupIndex = PickupCatalog.FindPickupIndex(equipmentDef.equipmentIndex);
                        }

                        combination[i] = ingredientPickupIndex;
                    }

                    allValidIngredientCombinations.Add(combination);
                }

                sb.AppendLine($"{craftableDef.name} ({craftableDef.GetPickupDefFromResult()?.pickupIndex ?? PickupIndex.none}):");
                
                foreach (PickupIndex[] ingredients in allValidIngredientCombinations)
                {
                    sb.AppendLine("\t" + string.Join(" + ", ingredients));
                }

                sb.AppendLine();
            }

            if (sb.Length > 0)
            {
                Log.Debug_NoCallerPrefix(sb);
            }
#endif
        }

        static UnityEngine.Object getPickupDefObject(PickupIndex pickupIndex)
        {
            PickupDef qualityResultPickup = PickupCatalog.GetPickupDef(pickupIndex);
            if (qualityResultPickup != null)
            {
                if (qualityResultPickup.itemIndex != ItemIndex.None)
                {
                    return ItemCatalog.GetItemDef(qualityResultPickup.itemIndex);
                }
                else if (qualityResultPickup.equipmentIndex != EquipmentIndex.None)
                {
                    return EquipmentCatalog.GetEquipmentDef(qualityResultPickup.equipmentIndex);
                }
            }

            return null;
        }

        static void CraftableCatalog_SetCraftableDefs(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            VariableDefinition allRecipesEnumeratorVar = null;
            VariableDefinition recipeEntryVar = null;

            bool recipeEntryLocalIndexMatchSuccess =
                c.TryGotoNext(MoveType.After,
                              x => x.MatchLdsfld(typeof(CraftableCatalog), nameof(CraftableCatalog.allRecipes)),
                              x => x.MatchCallOrCallvirt(out MethodReference m) && m?.Name == nameof(IEnumerable.GetEnumerator),
                              x => x.MatchStloc(il, out allRecipesEnumeratorVar))
                && c.TryGotoNext(MoveType.After,
                                 x => x.MatchLdloca(allRecipesEnumeratorVar),
                                 x => x.MatchCallOrCallvirt(out MethodReference m) && m?.Name == "get_" + nameof(IEnumerator.Current),
                                 x => x.MatchStloc(typeof(CraftableCatalog.RecipeEntry), il, out recipeEntryVar));

            if (!recipeEntryLocalIndexMatchSuccess)
            {
                Log.Error("Failed to find RecipeEntry loop variable");
                return;
            }

            VariableDefinition allPickupsEnumeratorVar = null;
            VariableDefinition pickupDefVar = null;

            bool pickupDefLocalIndexMatchSuccess =
                c.TryGotoNext(MoveType.After,
                              x => x.MatchLdloc(out _),
                              x => x.MatchCallOrCallvirt(out MethodReference m) && m?.Name == nameof(IEnumerable.GetEnumerator),
                              x => x.MatchStloc(il, out allPickupsEnumeratorVar))
                && c.TryGotoNext(MoveType.After,
                                 x => x.MatchLdloc(allPickupsEnumeratorVar),
                                 x => x.MatchCallOrCallvirt(out MethodReference m) && m?.Name == "get_" + nameof(IEnumerator.Current),
                                 x => x.MatchStloc(typeof(PickupDef), il, out pickupDefVar));

            if (!pickupDefLocalIndexMatchSuccess)
            {
                Log.Error("Failed to find PickupDef loop variable");
                return;
            }

            ILLabel ingredientInvalidLabel = null;
            if (!c.TryGotoNext(MoveType.After,
                               x => x.MatchCallOrCallvirt<RecipeIngredient>(nameof(RecipeIngredient.Validate)),
                               x => x.MatchBrfalse(out ingredientInvalidLabel)))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Emit(OpCodes.Ldloc, pickupDefVar);
            c.Emit(OpCodes.Ldloc, recipeEntryVar);
            c.EmitDelegate<Func<PickupDef, CraftableCatalog.RecipeEntry, bool>>(allowIngredient);
            c.Emit(OpCodes.Brfalse, ingredientInvalidLabel);

            static bool allowIngredient(PickupDef ingredientPickup, CraftableCatalog.RecipeEntry recipeEntry)
            {
                if (ingredientPickup == null || recipeEntry == null)
                    return true;

                bool ingredientIsQuality = QualityCatalog.GetQualityTier(ingredientPickup.pickupIndex) != QualityTier.None;
                bool resultIsQuality = QualityCatalog.GetQualityTier(recipeEntry.result) != QualityTier.None;
                bool recipeHasQuality = ingredientIsQuality || resultIsQuality;

                // If recipe contains quality items or result: Only allow if it's one of our defined recipes
                // If recipe contains no quality: Allow
                bool ingredientAllowed = !recipeHasQuality || _qualityCraftableDefs.Contains(recipeEntry.recipe.craftableDef);
                if (!ingredientAllowed)
                {
                    Log.Debug($"Not allowing ingredient {ingredientPickup.pickupIndex} for recipe {recipeEntry.recipe.craftableDef.name}[{recipeEntry.recipe.indexInCraftableDef}]");
                    return false;
                }

                return true;
            }
        }
    }
}
