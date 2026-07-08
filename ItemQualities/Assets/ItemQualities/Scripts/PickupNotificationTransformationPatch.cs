using ItemQualities.Utilities.Extensions;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using RoR2.Items;
using System;
using UnityEngine;

namespace ItemQualities
{
    internal static class PickupNotificationTransformationPatch
    {
        [SystemInitializer]
        private static void Init()
        {
            IL.RoR2.GenericPickupController.HandlePickupMessage += GenericPickupController_HandlePickupMessage;
        }

        private static void GenericPickupController_HandlePickupMessage(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            VariableDefinition masterObjectVar = null;
            if (!c.TryGotoNext(MoveType.Before,
                               x => x.MatchLdfld<GenericPickupController.PickupMessage>(nameof(GenericPickupController.PickupMessage.masterGameObject)),
                               x => x.MatchStloc(il, out masterObjectVar)))
            {
                Log.Error("Failed to find masterObject variable");
            }

            c.Goto(0);

            VariableDefinition pickupVar = null;
            if (!c.TryGotoNext(MoveType.Before,
                               x => x.MatchLdfld<GenericPickupController.PickupMessage>(nameof(GenericPickupController.PickupMessage.pickupState)),
                               x => x.MatchStloc(il, out pickupVar)))
            {
                Log.Error("Failed to find pickup variable");
            }

            c.Goto(0);

            if (!c.TryGotoNext(MoveType.Before,
                               x => x.MatchCallOrCallvirt<CharacterMasterNotificationQueue>(nameof(CharacterMasterNotificationQueue.PushPickupNotification))))
            {
                Log.Error("Failed to find PushPickupNotification call");
                return;
            }

            static bool matchBranchAny(Instruction instruction, out ILLabel label)
            {
                switch (instruction.OpCode.FlowControl)
                {
                    case FlowControl.Branch:
                    case FlowControl.Cond_Branch:
                        label = instruction.Operand as ILLabel;
                        break;
                    default:
                        label = default;
                        break;
                }

                return label != null;
            }

            Instruction pushPickupNotificationCallInstruction = c.Next;
            ILLabel skipPushPickupNotificationLabel = null;
            if (!c.TryGotoPrev(MoveType.After,
                               x => matchBranchAny(x, out skipPushPickupNotificationLabel) &&
                               il.IndexOf(skipPushPickupNotificationLabel.Target) > il.IndexOf(pushPickupNotificationCallInstruction)))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.MoveAfterLabels();

            c.Emit(OpCodes.Ldloc, masterObjectVar);
            c.Emit(OpCodes.Ldloc, pickupVar);
            c.EmitDelegate<Func<GameObject, UniquePickup, bool>>(shouldPushPickupNotification);
            c.Emit(OpCodes.Brfalse, skipPushPickupNotificationLabel);

            static bool shouldPushPickupNotification(GameObject masterObject, UniquePickup pickup)
            {
                if (!masterObject || !masterObject.TryGetComponent(out CharacterMaster master))
                    return true;

                PickupDef pickupDef = PickupCatalog.GetPickupDef(pickup.pickupIndex);
                if (pickupDef == null)
                    return true;

                CharacterMasterExtraStatsTracker masterExtraStats = masterObject.GetComponentCached<CharacterMasterExtraStatsTracker>();

                if (pickupDef.itemIndex != ItemIndex.None)
                {
                    // Don't show notification if the item will be corrupted
                    ItemIndex transformedItemIndex = ContagiousItemManager.GetTransformedItemIndex(pickupDef.itemIndex);
                    ItemQualityGroupIndex transformedItemGroupIndex = QualityCatalog.FindItemQualityGroupIndex(transformedItemIndex);
                    if (transformedItemGroupIndex != ItemQualityGroupIndex.Invalid &&
                        master.inventory.GetItemCountsEffective(transformedItemGroupIndex).TotalCount > 0)
                    {
                        return false;
                    }

                    // Don't show notification if item will be upgraded
                    if (masterExtraStats && masterExtraStats.HasUpgradeForItem(pickupDef.itemIndex))
                        return false;
                }

                return true;
            }
        }
    }
}
