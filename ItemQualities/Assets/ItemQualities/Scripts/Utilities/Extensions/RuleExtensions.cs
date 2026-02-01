using RoR2;

namespace ItemQualities.Utilities.Extensions
{
    static class RuleExtensions
    {
        public static bool IsPickupRuleEnabled(this RuleBook ruleBook, PickupIndex pickupIndex)
        {
            PickupDef pickupDef = PickupCatalog.GetPickupDef(pickupIndex);
            if (pickupDef == null)
                return false;

            if (pickupDef.itemIndex != ItemIndex.None)
            {
                return ruleBook.IsItemRuleEnabled(pickupDef.itemIndex);
            }
            else if (pickupDef.equipmentIndex != EquipmentIndex.None)
            {
                return ruleBook.IsEquipmentRuleEnabled(pickupDef.equipmentIndex);
            }

            return true;
        }

        public static bool IsItemRuleEnabled(this RuleBook ruleBook, ItemIndex itemIndex)
        {
            if (itemIndex == ItemIndex.None)
                return false;

            RuleDef ruleDef = RuleCatalog.FindRuleDef("Items." + ItemCatalog.GetItemDef(itemIndex).name);
            if (ruleDef == null)
                return true;
            
            RuleChoiceDef ruleChoiceDef = ruleBook.GetRuleChoice(ruleDef);
            if (ruleChoiceDef == null)
                return true;

            return ruleChoiceDef.itemIndex == itemIndex;
        }

        public static bool IsEquipmentRuleEnabled(this RuleBook ruleBook, EquipmentIndex equipmentIndex)
        {
            if (equipmentIndex == EquipmentIndex.None)
                return false;

            RuleDef ruleDef = RuleCatalog.FindRuleDef("Equipments." + EquipmentCatalog.GetEquipmentDef(equipmentIndex).name);
            if (ruleDef == null)
                return true;

            RuleChoiceDef ruleChoiceDef = ruleBook.GetRuleChoice(ruleDef);
            if (ruleChoiceDef == null)
                return true;

            return ruleChoiceDef.equipmentIndex == equipmentIndex;
        }
    }
}
