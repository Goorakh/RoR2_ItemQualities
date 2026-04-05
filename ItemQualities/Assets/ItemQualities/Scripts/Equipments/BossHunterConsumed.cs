using ItemQualities.Utilities.Extensions;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using System;

namespace ItemQualities.Equipments
{
    static class BossHunterConsumed
    {
        [SystemInitializer]
        static void Init()
        {
            IL.RoR2.EquipmentSlot.FireBossHunterConsumed += EquipmentSlot_FireBossHunterConsumed;
        }

        static void EquipmentSlot_FireBossHunterConsumed(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (!c.TryGotoNext(MoveType.Before,
                               x => x.MatchStfld<Chat.BodyChatMessage>(nameof(Chat.BodyChatMessage.token))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<string, EquipmentSlot, string>>(getChatMessageToken);
            static string getChatMessageToken(string token, EquipmentSlot self)
            {
                QualityTier qualityTier = self.GetCurrentEquipmentActionQualityTier();
                if (qualityTier != QualityTier.None)
                {
                    token = $"EQUIPMENT_BOSSHUNTERCONSUMED_{qualityTier.ToString().ToUpper()}_CHAT";
                }

                return token;
            }
        }
    }
}
