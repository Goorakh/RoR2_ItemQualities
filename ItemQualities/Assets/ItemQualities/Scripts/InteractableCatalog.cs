using HG;
using RoR2;
using RoR2.CharacterAI;
using RoR2.ContentManagement;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace ItemQualities
{
    static class InteractableCatalog
    {
        static InteractableDef[] _interactableDefs = Array.Empty<InteractableDef>();

        static readonly Dictionary<string, int> _interactableNameToIndex = new Dictionary<string, int>();

        public static int InteractableCount => _interactableDefs.Length;

        [SystemInitializer]
        static void Init()
        {
            using var _ = SetPool<GameObject>.RentCollection(out HashSet<GameObject> interactablePrefabs);
            interactablePrefabs.EnsureCapacity(ContentManager.networkedObjectPrefabs.Length);

            foreach (GameObject prefab in ContentManager.networkedObjectPrefabs)
            {
                if (prefab.GetComponent<CharacterBody>())
                    continue;

                if (prefab.GetComponent<GenericPickupController>())
                    continue;

                if (prefab.GetComponent<PickupPickerController>() &&
                    !prefab.GetComponent<DelusionChestController>() &&
                    !prefab.GetComponent<ScrapperController>() &&
                    !prefab.GetComponent<LemurianEggController>())
                {
                    continue;
                }

                if (prefab.GetComponent<DroneVendorTerminalBehavior>())
                    continue;

                // HACK: Filter out unused vending machine prefab, both have the same name so components seems like the best way to differentiate them
                if (prefab.GetComponent<VendingMachineBehavior>() && !prefab.GetComponent<AlignToNormal>())
                    continue;

                if (prefab.GetComponent<SpecialObjectAttributes>() ||
                    prefab.GetComponent<DroneVendorMultiShopController>() ||
                    prefab.GetComponent<IInteractable>() != null)
                {
                    interactablePrefabs.Add(prefab);
                    Log.Debug($"Including interactable prefab {prefab}");
                }
            }

            if (interactablePrefabs.Count > 0)
            {
                foreach (GameObject prefab in interactablePrefabs)
                {
                    prefab.EnsureComponent<InteractableInfoProvider>();
                }

                _interactableDefs = interactablePrefabs.Select(prefab => new InteractableDef(prefab)).ToArray();

                Array.Sort(_interactableDefs, Comparer<InteractableDef>.Create((a, b) => StringComparer.Ordinal.Compare(a.Name, b.Name)));

                _interactableNameToIndex.EnsureCapacity(_interactableDefs.Length);

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
                            prefab.GetComponent<ShrineColossusAccessBehavior>())
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
                    if (!_interactableNameToIndex.ContainsKey(interactableName))
                    {
                        _interactableNameToIndex[interactableName] = i;
                    }
                    else
                    {
                        Log.Warning($"Duplicate interactable name '{interactableName}'");
                    }
                }

                _interactableNameToIndex.TrimExcess();

                SpawnCard.onSpawnedServerGlobal += onSpawnCardSpawnedServerGlobal;

                On.RoR2.SpecialObjectAttributes.Start += SpecialObjectAttributes_Start;
                On.RoR2.PurchaseInteraction.Start += PurchaseInteraction_Start;
                On.RoR2.DroneVendorMultiShopController.Start += DroneVendorMultiShopController_Start;

                On.RoR2.ClassicStageInfo.Start += ClassicStageInfo_Start;

                static void overrideCanCopy(string interactableName, bool canCopy)
                {
                    InteractableDef interactableDef = GetInteractableDef(FindInteractableIndex(interactableName));
                    if (interactableDef != null)
                    {
                        interactableDef.CanCopy = canCopy;
                    }
                    else
                    {
                        Log.Warning($"Failed to find interactable '{interactableName}'");
                    }
                }

                overrideCanCopy("GoldshoresBeacon", false);
                overrideCanCopy("ScavBackpack", false);
                overrideCanCopy("ScavLunarBackpack", false);
            }
        }

        static void onSpawnCardSpawnedServerGlobal(SpawnCard.SpawnResult spawnResult)
        {
            if (spawnResult.spawnRequest.spawnCard is InteractableSpawnCard interactableSpawnCard)
            {
                recordSpawnCard(interactableSpawnCard);
            }
        }

        static void ClassicStageInfo_Start(On.RoR2.ClassicStageInfo.orig_Start orig, ClassicStageInfo self)
        {
            orig(self);

            try
            {
                if (self.interactableCategories)
                {
                    foreach (DirectorCardCategorySelection.Category category in self.interactableCategories.categories)
                    {
                        foreach (DirectorCard directorCard in category.cards)
                        {
                            SpawnCard spawnCard = directorCard.GetSpawnCard();
                            if (spawnCard && spawnCard is InteractableSpawnCard interactableSpawnCard && interactableSpawnCard.prefab)
                            {
                                recordSpawnCard(interactableSpawnCard);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warning_NoCallerPrefix(e);
            }
        }

        static void recordSpawnCard(InteractableSpawnCard spawnCard)
        {
            int interactableIndex = FindInteractableIndex(spawnCard.prefab);
            if (interactableIndex == -1)
            {
                Log.Debug($"Failed to find interactable index for '{spawnCard.prefab.name}'");
                return;
            }

            InteractableDef interactableDef = GetInteractableDef(interactableIndex);
            if (interactableDef != null && !interactableDef.SpawnCard)
            {
                interactableDef.SpawnCard = spawnCard;
                Log.Debug($"Recorded spawn card {spawnCard} for interactable {interactableDef.Name}");
            }
        }

        static void PurchaseInteraction_Start(On.RoR2.PurchaseInteraction.orig_Start orig, PurchaseInteraction self)
        {
            orig(self);
            tryLinkToCatalog(self.gameObject);
        }

        static void SpecialObjectAttributes_Start(On.RoR2.SpecialObjectAttributes.orig_Start orig, SpecialObjectAttributes self)
        {
            orig(self);

            if (!self.GetComponent<CharacterBody>())
            {
                tryLinkToCatalog(self.gameObject);
            }
        }

        static void DroneVendorMultiShopController_Start(On.RoR2.DroneVendorMultiShopController.orig_Start orig, DroneVendorMultiShopController self)
        {
            orig(self);
            tryLinkToCatalog(self.gameObject);
        }

        static void tryLinkToCatalog(GameObject interactableObject)
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

            string interactableName = interactableObject.name;
            if (interactableName.EndsWith("(Clone)", StringComparison.OrdinalIgnoreCase))
                interactableName = interactableName.Remove(interactableName.Length - 7);

            // Interactables that have been placed into a scene may have the 'number-in-parentheses' suffix. eg. "(1)", "(2)", etc
            interactableName = Regex.Replace(interactableName, @"\(\d+\)$", string.Empty);

            interactableName = interactableName.Trim();

            return FindInteractableIndex(interactableName);
        }

        public static int FindInteractableIndex(string interactableName)
        {
            return _interactableNameToIndex.GetValueOrDefault(interactableName, -1);
        }

        [ConCommand(commandName = "quality_list_clonable_interactables")]
        static void CCListClonableInteractables(ConCommandArgs args)
        {
            Debug.Log("=== Clonable Interactables ===");
            foreach (InteractableDef interactableDef in _interactableDefs)
            {
                if (interactableDef.CanCopy)
                {
                    Debug.Log(interactableDef.Name);
                }
            }

            Debug.Log("=== Non-Clonable Interactables ===");
            foreach (InteractableDef interactableDef in _interactableDefs)
            {
                if (!interactableDef.CanCopy)
                {
                    Debug.Log(interactableDef.Name);
                }
            }
        }
    }
}
