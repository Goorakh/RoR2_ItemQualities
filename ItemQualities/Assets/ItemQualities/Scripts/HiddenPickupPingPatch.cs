using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using System;

namespace ItemQualities
{
    static class HiddenPickupPingPatch
    {
        [SystemInitializer]
        static void Init()
        {
            IL.RoR2.UI.PingIndicator.GetFormattedTargetString += PingIndicator_GetFormattedTargetString;
        }

        static void PingIndicator_GetFormattedTargetString(ILContext il)
        {
            if (!il.Method.TryFindParameter<PickupIndex>(out ParameterDefinition pickupIndexParameter))
            {
                Log.Error("Failed to find PickupIndex parameter");
                return;
            }

            ILCursor c = new ILCursor(il);

            if (!c.TryGotoNext(MoveType.After,
                               x => x.MatchLdstr("?")))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Emit(OpCodes.Ldarg, pickupIndexParameter);
            c.EmitDelegate<Func<string, PickupIndex, string>>(getHiddenPickupName);

            static string getHiddenPickupName(string hiddenName, PickupIndex pickupIndex)
            {
                string name = hiddenName;

                QualityTier qualityTier = QualityCatalog.GetQualityTier(pickupIndex);
                if (qualityTier > QualityTier.None)
                {
                    QualityTierDef qualityTierDef = QualityCatalog.GetQualityTierDef(qualityTier);
                    name = Language.GetStringFormatted(qualityTierDef.modifierToken, name);
                }

                return name;
            }
        }
    }
}
