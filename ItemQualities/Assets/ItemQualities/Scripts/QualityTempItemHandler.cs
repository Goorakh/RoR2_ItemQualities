using EntityStates.Drifter;
using EntityStates.Drone.DroneJunk;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using System;
using UnityEngine;

namespace ItemQualities
{
    static class QualityTempItemHandler
    {
        [SystemInitializer]
        static void Init()
        {
            IL.EntityStates.Drone.DroneJunk.Surprise.DropTempItemServer += Surprise_DropTempItemServer;

            IL.EntityStates.Drifter.Salvage.DropTempItemServer += Salvage_DropTempItemServer;
        }

        static void Surprise_DropTempItemServer(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (!c.TryGotoNext(MoveType.After,
                               x => x.MatchCallOrCallvirt<PickupDropTable>(nameof(PickupDropTable.GeneratePickup))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<UniquePickup, Surprise, UniquePickup>>(clampPickupQuality);

            static UniquePickup clampPickupQuality(UniquePickup pickup, Surprise surpriseState)
            {
                QualityTier pickupQualityTier = QualityCatalog.GetQualityTier(pickup.pickupIndex);
                if (pickupQualityTier > QualityTier.None)
                {
                    CharacterBody droneBody = surpriseState?.characterBody;
                    CharacterMaster droneMaster = droneBody ? droneBody.master : null;
                    Inventory droneInventory = droneBody ? droneBody.inventory : null;

                    CharacterMaster droneOwnerMaster = droneMaster ? droneMaster.minionOwnership.ownerMaster : null;
                    PlayerCharacterMasterController droneOwnerPlayerMaster = droneOwnerMaster ? droneOwnerMaster.playerCharacterMasterController : null;
                    NetworkUser droneOwnerNetworkUser = droneOwnerPlayerMaster ? droneOwnerPlayerMaster.networkUser : null;
                    PickupDiscoveryNetworker droneOwnerPickupDiscovery = droneOwnerNetworkUser ? droneOwnerNetworkUser.GetComponent<PickupDiscoveryNetworker>() : null;

                    int droneTier = droneInventory ? droneInventory.GetItemCountEffective(DLC3Content.Items.DroneUpgradeHidden) : 0;
                    QualityTier maxPickupQualityTier = (QualityTier)Mathf.Clamp(droneTier - 1, (int)QualityTier.None, (int)QualityTier.Count - 1);

                    if (droneOwnerPickupDiscovery)
                    {
                        while (maxPickupQualityTier > QualityTier.None && !droneOwnerPickupDiscovery.HasDiscoveredPickup(QualityCatalog.GetPickupIndexOfQuality(pickup.pickupIndex, maxPickupQualityTier)))
                        {
                            maxPickupQualityTier--;
                        }
                    }
                    else
                    {
                        maxPickupQualityTier = QualityTier.None;
                    }

                    if (pickupQualityTier > maxPickupQualityTier)
                    {
                        PickupIndex newPickupIndex = QualityCatalog.GetPickupIndexOfQuality(pickup.pickupIndex, maxPickupQualityTier);
                        if (newPickupIndex.isValid)
                        {
                            Log.Debug($"Reducing junk drone pickup quality {pickupQualityTier} -> {maxPickupQualityTier} of {QualityCatalog.GetPickupIndexOfQuality(pickup.pickupIndex, QualityTier.None)}");

                            pickup.pickupIndex = newPickupIndex;
                            pickupQualityTier = maxPickupQualityTier;
                        }
                    }
                }

                return pickup;
            }
        }

        static void Salvage_DropTempItemServer(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (!c.TryGotoNext(MoveType.After,
                               x => x.MatchCallOrCallvirt<PickupDropTable>(nameof(PickupDropTable.GeneratePickup))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<UniquePickup, Salvage, UniquePickup>>(clampPickupQuality);

            static UniquePickup clampPickupQuality(UniquePickup pickup, Salvage salvageState)
            {
                QualityTier pickupQualityTier = QualityCatalog.GetQualityTier(pickup.pickupIndex);
                if (pickupQualityTier > QualityTier.None)
                {
                    CharacterBody body = salvageState?.characterBody;
                    Inventory inventory = body ? body.inventory : null;

                    NetworkUser networkUser = Util.LookUpBodyNetworkUser(body);
                    PickupDiscoveryNetworker pickupDiscovery = networkUser ? networkUser.GetComponent<PickupDiscoveryNetworker>() : null;

                    QualityTier maxPickupQualityTier = QualityTier.Count - 1;

                    if (pickupDiscovery)
                    {
                        while (maxPickupQualityTier > QualityTier.None && !pickupDiscovery.HasDiscoveredPickup(QualityCatalog.GetPickupIndexOfQuality(pickup.pickupIndex, maxPickupQualityTier)))
                        {
                            maxPickupQualityTier--;
                        }
                    }
                    else if (!body || body.isPlayerControlled)
                    {
                        maxPickupQualityTier = QualityTier.None;
                    }

                    if (pickupQualityTier > maxPickupQualityTier)
                    {
                        PickupIndex newPickupIndex = QualityCatalog.GetPickupIndexOfQuality(pickup.pickupIndex, maxPickupQualityTier);
                        if (newPickupIndex.isValid)
                        {
                            Log.Debug($"Reducing salvage pickup quality {pickupQualityTier} -> {maxPickupQualityTier} of {QualityCatalog.GetPickupIndexOfQuality(pickup.pickupIndex, QualityTier.None)}");

                            pickup.pickupIndex = newPickupIndex;
                            pickupQualityTier = maxPickupQualityTier;
                        }
                    }
                }

                return pickup;
            }
        }
    }
}
