using HG;
using RoR2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace ItemQualities
{
    internal static class InteractableCatalog
    {
        private static InteractableDef[] _interactableDefs = Array.Empty<InteractableDef>();

        private static readonly Dictionary<string, int> _interactablePrefabNameToIndex = new Dictionary<string, int>();

        public static int InteractableCount => _interactableDefs.Length;

        public static ResourceAvailability Availability;

        [InitDuringStartupPhase(GameInitPhase.PostProgressBar)]
        private static void Init()
        {
            InteractableSpawnCard[] spawnCards = Array.Empty<InteractableSpawnCard>();

            // Locate spawn cards
            {
                HashSet<InteractableSpawnCard> loadedSpawnCards = new HashSet<InteractableSpawnCard>();

                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                // First, find all spawn cards loaded into memory, this should catch most modded cards
                loadedSpawnCards.UnionWith(Resources.FindObjectsOfTypeAll<InteractableSpawnCard>());

                // Then, find all InteractableSpawnCards in Addressables, surprisingly not horribly inefficient
                // TODO: convert this to async
                try
                {
                    List<IResourceLocation> interactableSpawnCardLocations = new List<IResourceLocation>();
                    foreach (IResourceLocator locator in Addressables.ResourceLocators)
                    {
                        if (locator is not ResourceLocationMap resourceLocationMap)
                            continue;

                        foreach ((object key, IList<IResourceLocation> resourceLocations) in resourceLocationMap.Locations)
                        {
                            if (resourceLocations == null || resourceLocations.Count == 0)
                                continue;

                            foreach (IResourceLocation resourceLocation in resourceLocations)
                            {
                                if (typeof(InteractableSpawnCard).IsAssignableFrom(resourceLocation.ResourceType))
                                {
                                    ListUtils.AddIfUnique(interactableSpawnCardLocations, resourceLocation);
                                }
                            }
                        }
                    }

                    if (interactableSpawnCardLocations.Count > 0)
                    {
                        var interactableSpawnCardsHandle = Addressables.LoadAssetsAsync<InteractableSpawnCard>(
                            interactableSpawnCardLocations,
                            null,
                            false);

                        interactableSpawnCardsHandle.WaitForCompletion();

                        if (interactableSpawnCardsHandle.Status == AsyncOperationStatus.Succeeded && interactableSpawnCardsHandle.Result != null)
                        {
                            loadedSpawnCards.UnionWith(interactableSpawnCardsHandle.Result);
                            Addressables.Release(interactableSpawnCardsHandle);
                        }
                        else
                        {
                            Log.Warning($"InteractableSpawnCard loads failed: {interactableSpawnCardsHandle.OperationException}");
                        }
                    }
                }
                catch (Exception e)
                {
                    Log.Warning_NoCallerPrefix($"Failed to load InteractableSpawnCards from addressables: {e}");
                }

                Log.Debug($"Found {loadedSpawnCards.Count} total InteractableSpawnCards in {stopwatch.Elapsed.TotalMilliseconds:F0}ms");

                if (loadedSpawnCards.Count > 0)
                {
                    spawnCards = loadedSpawnCards.Where(c => c.prefab)
                                                 .OrderBy(c => c.name)
                                                 .ToArray();
                }
            }

            // Catalog spawn cards
            if (spawnCards.Length > 0)
            {
                foreach (InteractableSpawnCard card in spawnCards)
                {
                    card.prefab.EnsureComponent<InteractableInfoProvider>();
                }

                _interactableDefs = spawnCards.Select(spawnCard => new InteractableDef(spawnCard))
                                              .ToArray();

                Array.Sort(_interactableDefs, Comparer<InteractableDef>.Create((a, b) => StringComparer.Ordinal.Compare(a.Name, b.Name)));

                _interactablePrefabNameToIndex.EnsureCapacity(_interactableDefs.Length);

                for (int i = 0; i < _interactableDefs.Length; i++)
                {
                    InteractableDef interactableDef = _interactableDefs[i];
                    GameObject interactablePrefab = interactableDef.Prefab;
                    InteractableInfoProvider interactableInfoComponent = interactableDef.PrefabInfoProviderComponent;

                    interactableInfoComponent.CatalogIndex = i;

                    static bool allowCopy(GameObject prefab)
                    {
                        if (prefab.GetComponent<PortalSpawner>() ||
                            prefab.GetComponent<SceneExitController>() ||
                            prefab.GetComponent<HoldoutZoneController>() ||
                            prefab.GetComponent<GeodeController>() ||
                            prefab.GetComponent<ShrineColossusAccessBehavior>() ||
                            prefab.GetComponent<CharacterMaster>() ||
                            prefab.GetComponent<CharacterBody>())
                        {
                            return false;
                        }

                        return true;
                    }

                    if (!allowCopy(interactablePrefab))
                    {
                        interactableDef.CanCopy = false;
                        Log.Debug($"Disabled copying for interactable '{interactableDef}'");
                    }

                    string interactableName = interactableDef.Name;
                    if (!_interactablePrefabNameToIndex.ContainsKey(interactableName))
                    {
                        _interactablePrefabNameToIndex[interactableName] = i;
                    }
                    else
                    {
                        Log.Warning($"Duplicate interactable name '{interactableName}'");
                    }
                }

                _interactablePrefabNameToIndex.TrimExcess();

                On.RoR2.SpecialObjectAttributes.Start += SpecialObjectAttributes_Start;
                On.RoR2.PurchaseInteraction.Start += PurchaseInteraction_Start;
                On.RoR2.DroneVendorMultiShopController.Start += DroneVendorMultiShopController_Start;

                static void overrideCanCopy(string interactablePrefabName, bool canCopy)
                {
                    InteractableDef interactableDef = GetInteractableDef(FindInteractableIndex(interactablePrefabName));
                    if (interactableDef != null)
                    {
                        interactableDef.CanCopy = canCopy;
                    }
                    else
                    {
                        Log.Warning($"Failed to find interactable '{interactablePrefabName}'");
                    }
                }

                overrideCanCopy("GauntletEntranceOrb", false);
                overrideCanCopy("GoldshoresBeacon", false);
                overrideCanCopy("ScavBackpack", false);
                overrideCanCopy("ScavLunarBackpack", false);
                overrideCanCopy("VoidCamp", false); // lol
                overrideCanCopy("VoidRaidSafeWard", false);
            }

            Availability.MakeAvailable();
        }

        private static void PurchaseInteraction_Start(On.RoR2.PurchaseInteraction.orig_Start orig, PurchaseInteraction self)
        {
            orig(self);
            tryLinkToCatalog(self.gameObject);
        }

        private static void SpecialObjectAttributes_Start(On.RoR2.SpecialObjectAttributes.orig_Start orig, SpecialObjectAttributes self)
        {
            orig(self);

            if (!self.GetComponent<CharacterBody>())
            {
                tryLinkToCatalog(self.gameObject);
            }
        }

        private static void DroneVendorMultiShopController_Start(On.RoR2.DroneVendorMultiShopController.orig_Start orig, DroneVendorMultiShopController self)
        {
            orig(self);
            tryLinkToCatalog(self.gameObject);
        }

        private static void tryLinkToCatalog(GameObject interactableObject)
        {
            int interactableIndex = FindInteractableIndex(interactableObject);
            if (interactableIndex == -1)
            {
                Log.Debug($"Failed to find interactable index for '{interactableObject.name}'");
                return;
            }

            if (!interactableObject.TryGetComponent(out InteractableInfoProvider catalogedInteractable))
            {
                catalogedInteractable = interactableObject.AddComponent<InteractableInfoProvider>();
                catalogedInteractable.CatalogIndex = interactableIndex;
            }
        }

        public static InteractableDef GetInteractableDef(int interactableIndex)
        {
            return ArrayUtils.GetSafe(_interactableDefs, interactableIndex);
        }

        public static int FindInteractableIndex(GameObject interactableObject)
        {
            if (interactableObject.TryGetComponent(out InteractableInfoProvider catalogedInteractable))
            {
                return catalogedInteractable.CatalogIndex;
            }

            string interactablePrefabName = interactableObject.name;
            if (interactablePrefabName.EndsWith("(Clone)", StringComparison.OrdinalIgnoreCase))
                interactablePrefabName = interactablePrefabName.Remove(interactablePrefabName.Length - 7);

            // Interactables that have been placed into a scene may have the 'number-in-parentheses' suffix. eg. "(1)", "(2)", etc
            // Also remove " - X" (where x is any number) suffix, used on gilded coast preplaced chests
            interactablePrefabName = Regex.Replace(interactablePrefabName, @"\s*(-\s*\d+)?\s*(\(\d+\))?\s*$", string.Empty);

            interactablePrefabName = interactablePrefabName.Trim();

            return FindInteractableIndex(interactablePrefabName);
        }

        public static int FindInteractableIndex(string interactablePrefabName)
        {
            return _interactablePrefabNameToIndex.GetValueOrDefault(interactablePrefabName, -1);
        }

        [ConCommand(commandName = "quality_list_clonable_interactables")]
        private static void CCListClonableInteractables(ConCommandArgs args)
        {
            args.Log("=== Clonable Interactables ===");
            foreach (InteractableDef interactableDef in _interactableDefs)
            {
                if (interactableDef.CanCopy)
                {
                    args.Log(interactableDef.Name);
                }
            }

            args.Log("=== Non-Clonable Interactables ===");
            foreach (InteractableDef interactableDef in _interactableDefs)
            {
                if (!interactableDef.CanCopy)
                {
                    args.Log(interactableDef.Name);
                }
            }
        }
    }
}
