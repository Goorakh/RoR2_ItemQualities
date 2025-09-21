using RoR2;

namespace ItemQualities.Equipments
{
    static class BossHunterConsumed
    {
        [SystemInitializer]
        static void Init()
        {
            EquipmentHooks.RegisterEquipmentGroupAction(ItemQualitiesContent.EquipmentQualityGroups.BossHunterConsumed, performAction);
        }

        static bool performAction(EquipmentSlot equipmentSlot, QualityTier qualityTier)
        {
            if (!equipmentSlot.characterBody)
                return false;

            Chat.SendBroadcastChat(new Chat.BodyChatMessage
            {
                bodyObject = equipmentSlot.characterBody.gameObject,
                token = $"EQUIPMENT_BOSSHUNTERCONSUMED_{qualityTier.ToString().ToUpper()}_CHAT"
            });

            return true;
        }
    }
}
