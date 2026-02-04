using HG;
using RoR2;
using RoR2.ContentManagement;
using System;
using System.Collections.Generic;
using System.Linq;
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

                if (prefab.GetComponent<PickupPickerController>() && !prefab.GetComponent<DelusionChestController>() && !prefab.GetComponent<ScrapperController>())
                    continue;

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
                _interactableDefs = interactablePrefabs.Select(prefab => new InteractableDef(prefab)).ToArray();

                Array.Sort(_interactableDefs, Comparer<InteractableDef>.Create((a, b) => StringComparer.Ordinal.Compare(a.Name, b.Name)));

                _interactableNameToIndex.EnsureCapacity(_interactableDefs.Length);

                for (int i = 0; i < _interactableDefs.Length; i++)
                {
                    _interactableDefs[i].InteractableIndex = i;

                    string name = _interactableDefs[i].Name;

                    if (_interactableNameToIndex.ContainsKey(name))
                    {
                        Log.Warning($"Duplicate interactable name '{name}'");
                        continue;
                    }

                    _interactableNameToIndex[name] = i;
                }

                _interactableNameToIndex.TrimExcess();

                On.RoR2.SpecialObjectAttributes.Start += SpecialObjectAttributes_Start;
                On.RoR2.PurchaseInteraction.Start += PurchaseInteraction_Start;
                On.RoR2.DroneVendorMultiShopController.Start += DroneVendorMultiShopController_Start;

                On.RoR2.ClassicStageInfo.Start += ClassicStageInfo_Start;

                InteractableDef freeChestMultiShop = GetInteractableDef(FindInteractableIndex("FreeChestMultiShop"));
                if (freeChestMultiShop != null)
                {
                    freeChestMultiShop.CanCopy = false;
                }
                else
                {
                    Log.Warning("Failed to find interactable FreeChestMultiShop");
                }
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
                                int interactableIndex = FindInteractableIndex(interactableSpawnCard.prefab);
                                if (interactableIndex == -1)
                                {
                                    Log.Debug($"Failed to find interactable index for '{interactableSpawnCard.prefab.name}'");
                                    continue;
                                }

                                InteractableDef interactableDef = GetInteractableDef(interactableIndex);
                                if (interactableDef != null && !interactableDef.SpawnCard)
                                {
                                    interactableDef.SpawnCard = interactableSpawnCard;
                                    Log.Debug($"Recorded spawn card {interactableSpawnCard} for interactable {interactableDef.Name}");
                                }
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

        static void PurchaseInteraction_Start(On.RoR2.PurchaseInteraction.orig_Start orig, PurchaseInteraction self)
        {
            orig(self);
            tryLinkToCatalog(self.gameObject);
        }

        static void SpecialObjectAttributes_Start(On.RoR2.SpecialObjectAttributes.orig_Start orig, SpecialObjectAttributes self)
        {
            orig(self);
            tryLinkToCatalog(self.gameObject);
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

            if (!interactableObject.TryGetComponent(out CatalogedInteractable catalogedInteractable))
            {
                catalogedInteractable = interactableObject.AddComponent<CatalogedInteractable>();
                catalogedInteractable.CatalogIndex = interactableIndex;
            }
        }

        public static InteractableDef GetInteractableDef(int interactableIndex)
        {
            return ArrayUtils.GetSafe(_interactableDefs, interactableIndex);
        }

        public static int FindInteractableIndex(GameObject interactableObject)
        {
            if (interactableObject.TryGetComponent(out CatalogedInteractable catalogedInteractable))
            {
                return catalogedInteractable.CatalogIndex;
            }

            string interactableName = interactableObject.name;
            if (interactableName.EndsWith("(Clone)"))
                interactableName = interactableName.Remove(interactableName.Length - 7);

            return FindInteractableIndex(interactableName);
        }

        public static int FindInteractableIndex(string interactableName)
        {
            return _interactableNameToIndex.GetValueOrDefault(interactableName, -1);
        }
    }
}
