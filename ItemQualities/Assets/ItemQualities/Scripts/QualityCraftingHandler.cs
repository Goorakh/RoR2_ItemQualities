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
using System.Linq;
using UnityEngine;

namespace ItemQualities
{
    static class QualityCraftingHandler
    {
        static CraftableDef[] _qualityCraftableDefs = Array.Empty<CraftableDef>();

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
            if (_qualityCraftableDefs.Length > 0)
            {
                foreach (CraftableDef qualityCraftableDef in _qualityCraftableDefs)
                {
                    CraftableDef.Destroy(qualityCraftableDef);
                }

                _qualityCraftableDefs = Array.Empty<CraftableDef>();
            }

            // Would be nice to just append these to the content pack directly,
            // but we need to use the RecipeIngredient.Validate method to resolve indices, which depends on some catalogs,
            // so we need to do this after PickupCatalog and IngredientTypeCatalog are initialized.

            List<CraftableDef> qualityCraftableDefs = new List<CraftableDef>(allCraftableDefs.Length * (int)QualityTier.Count);

            foreach (CraftableDef craftableDef in allCraftableDefs)
            {
                if (!craftableDef)
                    continue;

                PickupDef resultPickup = craftableDef.GetPickupDefFromResult();
                PickupIndex resultPickupIndex = resultPickup != null ? resultPickup.pickupIndex : PickupIndex.none;
                if (!resultPickupIndex.isValid)
                    continue;

                Span<List<Recipe>> qualityRecipiesByResultQuality = new List<Recipe>[(int)QualityTier.Count];

                foreach (Recipe recipe in craftableDef.recipes)
                {
                    int ingredientSlotCount = recipe?.ingredients != null ? recipe.ingredients.Length : 0;
                    if (ingredientSlotCount == 0)
                        continue;

                    Span<PickupIndex[]> possibleIngredientsBySlot = new PickupIndex[ingredientSlotCount][];

                    bool allSlotsHaveIngredients = true;

                    for (int i = 0; i < ingredientSlotCount; i++)
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

                        HashSet<PickupIndex> validIngredients = new HashSet<PickupIndex>();

                        foreach (PickupIndex ingredientPickupIndex in PickupCatalog.allPickupIndices)
                        {
                            QualityTier ingredientQualityTier = QualityCatalog.GetQualityTier(ingredientPickupIndex);
                            if (ingredientQualityTier > QualityTier.None)
                            {
                                PickupIndex baseIngredientPickupIndex = QualityCatalog.GetPickupIndexOfQuality(ingredientPickupIndex, QualityTier.None);

                                if (ingredient.Validate(baseIngredientPickupIndex))
                                {
                                    validIngredients.Add(ingredientPickupIndex);
                                }
                            }
                        }

                        if (validIngredients.Count > 0)
                        {
                            possibleIngredientsBySlot[i] = validIngredients.ToArray();
                        }
                        else
                        {
                            allSlotsHaveIngredients = false;
                            break;
                        }
                    }

                    if (!allSlotsHaveIngredients)
                        continue;

                    Span<int> slotIngredientIndices = stackalloc int[ingredientSlotCount];

                    bool hasVisitedAllIngredientPermutations;
                    do
                    {
                        int totalIngredientsQualityTierValues = 0;

                        Span<PickupIndex> permutationIngredients = stackalloc PickupIndex[ingredientSlotCount];
                        for (int i = 0; i < ingredientSlotCount; i++)
                        {
                            PickupIndex ingredientPickupIndex = possibleIngredientsBySlot[i][slotIngredientIndices[i]];
                            permutationIngredients[i] = ingredientPickupIndex;
                            totalIngredientsQualityTierValues += (int)QualityCatalog.GetQualityTier(ingredientPickupIndex);
                        }

                        QualityTier resultQualityTier = (QualityTier)(totalIngredientsQualityTierValues / (float)ingredientSlotCount);
                        if (resultQualityTier > QualityTier.None && resultQualityTier < QualityTier.Count)
                        {
                            bool allIngredientsValid = true;

                            Span<RecipeIngredient> ingredients = new RecipeIngredient[ingredientSlotCount];
                            for (int i = 0; i < ingredientSlotCount; i++)
                            {
                                UnityEngine.Object ingredientPickup = getPickupDefObject(permutationIngredients[i]);
                                if (!ingredientPickup)
                                {
                                    Log.Warning($"Failed to find pickup object for {permutationIngredients[i]}");
                                    allIngredientsValid = false;
                                    break;
                                }

                                ingredients[i] = new RecipeIngredient
                                {
                                    type = IngredientTypeIndex.AssetReference,
                                    pickup = ingredientPickup
                                };
                            }

                            if (allIngredientsValid)
                            {
                                List<Recipe> qualityRecipes = qualityRecipiesByResultQuality[(int)resultQualityTier] ??= new List<Recipe>();

                                qualityRecipes.Add(new Recipe
                                {
                                    ingredients = ingredients.ToArray(),
                                    amountToDrop = recipe.amountToDrop,
                                    priority = recipe.priority,
                                });
                            }
                        }

                        bool successfullyIncrementedIndex = false;
                        for (int i = 0; i < ingredientSlotCount; i++)
                        {
                            if (slotIngredientIndices[i] < possibleIngredientsBySlot[i].Length - 1)
                            {
                                slotIngredientIndices[i]++;

                                if (i > 0)
                                {
                                    slotIngredientIndices.Slice(0, i).Fill(0);
                                }

                                successfullyIncrementedIndex = true;

                                break;
                            }
                        }

                        hasVisitedAllIngredientPermutations = !successfullyIncrementedIndex;
                    } while (!hasVisitedAllIngredientPermutations);
                }

                for (QualityTier qualityTier = 0; qualityTier < QualityTier.Count; qualityTier++)
                {
                    List<Recipe> qualityRecipes = qualityRecipiesByResultQuality[(int)qualityTier];
                    if (qualityRecipes != null && qualityRecipes.Count > 0)
                    {
                        PickupIndex qualityResultPickupIndex = QualityCatalog.GetPickupIndexOfQuality(resultPickupIndex, qualityTier);
                        if (resultPickupIndex != qualityResultPickupIndex)
                        {
                            UnityEngine.Object qualityResultPickupDefObject = getPickupDefObject(qualityResultPickupIndex);
                            if (qualityResultPickupDefObject)
                            {
                                CraftableDef qualityCraftableDef = ScriptableObject.CreateInstance<CraftableDef>();
                                qualityCraftableDef.name = $"{craftableDef.name}{qualityTier}";
                                qualityCraftableDef.pickup = qualityResultPickupDefObject;
                                qualityCraftableDef.recipes = qualityRecipes.ToArray();

                                qualityCraftableDefs.Add(qualityCraftableDef);
                            }
                        }
                    }
                }
            }

            if (qualityCraftableDefs.Count > 0)
            {
                _qualityCraftableDefs = qualityCraftableDefs.ToArray();

                int baseCraftableDefsCount = allCraftableDefs.Length;
                Array.Resize(ref allCraftableDefs, baseCraftableDefsCount + qualityCraftableDefs.Count);
                qualityCraftableDefs.CopyTo(allCraftableDefs, baseCraftableDefsCount);

                Log.Debug($"Added {qualityCraftableDefs.Count} quality CraftableDef(s)");
            }
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

            int allRecipesEnumeratorLocalIndex = -1;
            int recipeEntryLocalIndex = -1;

            bool recipeEntryLocalIndexMatchSuccess =
                c.TryGotoNext(MoveType.After,
                              x => x.MatchLdsfld(typeof(CraftableCatalog), nameof(CraftableCatalog.allRecipes)),
                              x => x.MatchCallOrCallvirt(out MethodReference m) && m?.Name == nameof(IEnumerable.GetEnumerator),
                              x => x.MatchStloc(out allRecipesEnumeratorLocalIndex))
                && c.TryGotoNext(MoveType.After,
                                 x => x.MatchLdloca(allRecipesEnumeratorLocalIndex),
                                 x => x.MatchCallOrCallvirt(out MethodReference m) && m?.Name == "get_" + nameof(IEnumerator.Current),
                                 x => x.MatchStloc(typeof(CraftableCatalog.RecipeEntry), il, out recipeEntryLocalIndex));

            if (!recipeEntryLocalIndexMatchSuccess)
            {
                Log.Error("Failed to find RecipeEntry loop variable");
                return;
            }

            int allPickupsEnumeratorLocalIndex = -1;
            int pickupDefLocalIndex = -1;

            bool pickupDefLocalIndexMatchSuccess =
                c.TryGotoNext(MoveType.After,
                              x => x.MatchLdloc(out _),
                              x => x.MatchCallOrCallvirt(out MethodReference m) && m?.Name == nameof(IEnumerable.GetEnumerator),
                              x => x.MatchStloc(out allPickupsEnumeratorLocalIndex))
                && c.TryGotoNext(MoveType.After,
                                 x => x.MatchLdloc(allPickupsEnumeratorLocalIndex),
                                 x => x.MatchCallOrCallvirt(out MethodReference m) && m?.Name == "get_" + nameof(IEnumerator.Current),
                                 x => x.MatchStloc(typeof(PickupDef), il, out pickupDefLocalIndex));

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

            c.Emit(OpCodes.Ldloc, pickupDefLocalIndex);
            c.Emit(OpCodes.Ldloc, recipeEntryLocalIndex);
            c.EmitDelegate<Func<PickupDef, CraftableCatalog.RecipeEntry, bool>>(allowIngredient);
            c.Emit(OpCodes.Brfalse, ingredientInvalidLabel);

            static bool allowIngredient(PickupDef ingredientPickup, CraftableCatalog.RecipeEntry recipeEntry)
            {
                if (ingredientPickup == null || recipeEntry == null)
                    return true;

                bool ingredientIsQuality = QualityCatalog.GetQualityTier(ingredientPickup.pickupIndex) != QualityTier.None;
                bool recipeIsQuality = QualityCatalog.GetQualityTier(recipeEntry.result) != QualityTier.None;

                return ingredientIsQuality == recipeIsQuality;
            }
        }
    }
}
