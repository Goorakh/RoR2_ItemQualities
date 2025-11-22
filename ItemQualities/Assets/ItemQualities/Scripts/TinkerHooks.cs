using ItemQualities.Items;
using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using RoR2;
using System;
using System.Reflection;

namespace ItemQualities
{
    static class TinkerHooks
    {
        [SystemInitializer]
        static void Init()
        {
            IL.RoR2.Projectile.TinkerProjectile.TransmuteTargetObject += Scrap.ReplaceScrapPickupFromItemDefTierQualityPatch;

            IL.RoR2.DrifterTracker.IsWhitelist += DrifterTracker_IsWhitelist;

            PropertyInfo isTinkerableProperty = typeof(TinkerableObjectAttributes).GetProperty(nameof(TinkerableObjectAttributes.IsTinkerable));
            if (isTinkerableProperty?.GetMethod != null)
            {
                new Hook(isTinkerableProperty.GetMethod, new Func<orig_TinkerableObjectAttributes_get_IsTinkerable, TinkerableObjectAttributes, bool>(TinkerableObjectAttributes_get_IsTinkerable));
            }
            else
            {
                Log.Error("Failed to find TinkerableObjectAttributes.IsTinkerable getter method");
            }

            IL.RoR2.PickupPickerController.RerollCurrentOptions += PickupPickerController_RerollCurrentOptions;

            On.RoR2.ShopTerminalBehavior.GenerateReroll += ShopTerminalBehavior_GenerateReroll;
        }

        static bool interactablePickupIsTinkerable(in UniquePickup pickup)
        {
            return pickup.isValid && QualityCatalog.GetQualityTier(pickup.pickupIndex) == QualityTier.None;
        }

        delegate bool orig_TinkerableObjectAttributes_get_IsTinkerable(TinkerableObjectAttributes self);
        static bool TinkerableObjectAttributes_get_IsTinkerable(orig_TinkerableObjectAttributes_get_IsTinkerable orig, TinkerableObjectAttributes self)
        {
            if (!orig(self))
                return false;

            if (self.shopBehavior)
            {
                if (!interactablePickupIsTinkerable(self.shopBehavior.CurrentPickup()))
                    return false;
            }
            else if (self.TryGetComponent(out PickupDistributorBehavior pickupDistributorBehavior))
            {
                if (!interactablePickupIsTinkerable(pickupDistributorBehavior.pickup))
                    return false;
            }
            else if (self.TryGetComponent(out PickupPickerController pickupPickerController))
            {
                bool anyPickupTinkerable = false;
                foreach (PickupPickerController.Option option in pickupPickerController.options)
                {
                    bool isTinkerable = interactablePickupIsTinkerable(option.pickup);
                    if (isTinkerable)
                    {
                        anyPickupTinkerable = true;
                        break;
                    }
                }

                if (!anyPickupTinkerable)
                    return false;
            }

            return true;
        }

        static UniquePickup? ShopTerminalBehavior_GenerateReroll(On.RoR2.ShopTerminalBehavior.orig_GenerateReroll orig, PickupDropTable dropTable, Xoroshiro128Plus rng, UniquePickup pickup)
        {
            UniquePickup? result = orig(dropTable, rng, pickup);

            if (result.HasValue && result.Value.isValid && pickup.isValid)
            {
                result = result.Value.WithQualityTier(QualityCatalog.GetQualityTier(pickup.pickupIndex));
            }

            return result;
        }

        static void PickupPickerController_RerollCurrentOptions(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            c.Index = 0;
            if (c.TryGotoNext(MoveType.Before,
                              x => x.MatchCallOrCallvirt(typeof(PickupTransmutationManager), nameof(PickupTransmutationManager.GetAvailableGroupFromPickupIndex))))
            {
                c.Emit(OpCodes.Dup);
                c.Index++; // Move after call

                c.EmitDelegate<Func<PickupIndex, PickupIndex[], PickupIndex[]>>(getPickupTransmutationGroup);

                static PickupIndex[] getPickupTransmutationGroup(PickupIndex pickupIndex, PickupIndex[] group)
                {
                    return QualityCatalog.GetQualityTier(pickupIndex) == QualityTier.None ? group : Array.Empty<PickupIndex>();
                }
            }
            else
            {
                Log.Error("Failed to find transmutation group patch location");
            }

            // Fix for-loop returning rather than continuing when it encounters an option with no transmutation options
            c.Index = 0;
            if (c.TryGotoNext(MoveType.After,
                              x => x.MatchCallOrCallvirt<PickupPickerController>(nameof(PickupPickerController.SetOptionsServer))))
            {
                ILLabel loopContinueLabel = c.MarkLabel();

                int patchCount = 0;

                while (c.TryGotoPrev(MoveType.Before,
                                     x => x.MatchRet()))
                {
                    c.Emit(OpCodes.Br, loopContinueLabel);
                    patchCount++;
                }

                if (patchCount == 0)
                {
                    Log.Warning("Failed to find loop exit fix patch location");
                }
                else
                {
                    Log.Debug($"Found {patchCount} loop exit fix patch location(s)");
                }
            }
            else
            {
                Log.Error("Failed to find loop end location");
            }
        }

        static void DrifterTracker_IsWhitelist(ILContext il)
        {
            if (!il.Method.TryFindParameter(typeof(UniquePickup).MakeByRefType(), out ParameterDefinition pickupParameter))
            {
                Log.Error("Failed to find pickup parameter");
                return;
            }

            ILCursor c = new ILCursor(il);

            if (!c.TryFindNext(out ILCursor[] foundCursors,
                               x => x.MatchLdcI4((int)ItemTag.WorldUnique),
                               x => x.MatchCallOrCallvirt<ItemDef>(nameof(ItemDef.ContainsTag))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Goto(foundCursors[1].Next, MoveType.After); // call ItemDef.ContainsTag

            c.Emit(OpCodes.Ldarg, pickupParameter);
            c.EmitDelegate<GetBaseQualityIsWorldUniqueDelegate>(getBaseQualityIsWorldUnique);

            static bool getBaseQualityIsWorldUnique(bool isWorldUnique, in UniquePickup pickup)
            {
                PickupIndex basePickupIndex = QualityCatalog.GetPickupIndexOfQuality(pickup.pickupIndex, QualityTier.None);
                if (basePickupIndex == pickup.pickupIndex)
                    return isWorldUnique;

                PickupDef basePickupDef = PickupCatalog.GetPickupDef(basePickupIndex);
                ItemDef baseItemDef = basePickupDef != null ? ItemCatalog.GetItemDef(basePickupDef.itemIndex) : null;
                if (!baseItemDef)
                    return isWorldUnique;

                return baseItemDef.ContainsTag(ItemTag.WorldUnique);
            }
        }

        delegate bool GetBaseQualityIsWorldUniqueDelegate(bool isWorldUnique, in UniquePickup pickup);
    }
}
