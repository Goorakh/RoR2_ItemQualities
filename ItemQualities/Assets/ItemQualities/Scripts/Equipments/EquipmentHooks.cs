using RoR2;
using System.Collections.Generic;

namespace ItemQualities.Equipments
{
    static class EquipmentHooks
    {
        public delegate bool EquipmentActivationDelegate(EquipmentSlot equipmentSlot, QualityTier qualityTier);

        class EquipmentAction
        {
            public EquipmentQualityGroupIndex EquipmentGroupIndex { get; }

            public EquipmentActivationDelegate ActivationDelegate { get; }

            public EquipmentAction(EquipmentQualityGroupIndex equipmentGroupIndex, EquipmentActivationDelegate activationDelegate)
            {
                EquipmentGroupIndex = equipmentGroupIndex;
                ActivationDelegate = activationDelegate;
            }
        }

        static readonly List<EquipmentAction> _equipmentActions = new List<EquipmentAction>();

        public static void RegisterEquipmentGroupAction(EquipmentQualityGroup equipmentGroup, EquipmentActivationDelegate activationDelegate)
        {
            QualityCatalog.Availability.CallWhenAvailable(() =>
            {
                _equipmentActions.Add(new EquipmentAction(equipmentGroup.GroupIndex, activationDelegate));
            });
        }

        [SystemInitializer]
        static void Init()
        {
            On.RoR2.EquipmentSlot.PerformEquipmentAction += EquipmentSlot_PerformEquipmentAction;
        }

        static bool EquipmentSlot_PerformEquipmentAction(On.RoR2.EquipmentSlot.orig_PerformEquipmentAction orig, EquipmentSlot self, EquipmentDef equipmentDef)
        {
            bool result = orig(self, equipmentDef);

            if (self && equipmentDef && !result && !self.equipmentDisabled)
            {
                QualityTier qualityTier = QualityCatalog.GetQualityTier(equipmentDef.equipmentIndex);
                if (qualityTier > QualityTier.None)
                {
                    EquipmentQualityGroupIndex equipmentGroupIndex = QualityCatalog.FindEquipmentQualityGroupIndex(equipmentDef.equipmentIndex);

                    foreach (EquipmentAction action in _equipmentActions)
                    {
                        if (action.EquipmentGroupIndex == equipmentGroupIndex)
                        {
                            result = action.ActivationDelegate(self, qualityTier);
                            break;
                        }
                    }
                }
            }

            return result;
        }
    }
}
