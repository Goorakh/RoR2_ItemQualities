using HG;
using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using RoR2;
using RoR2BepInExPack.GameAssetPathsBetter;
using UnityEngine;

namespace ItemQualities.Equipments
{
    static class Jetpack
    {
        [SystemInitializer]
        static void Init()
        {
            AddressableUtil.LoadAssetAsync<GameObject>(RoR2_Base_Jetpack.JetpackController_prefab).OnSuccess(jetpackAttachment =>
            {
                jetpackAttachment.EnsureComponent<JetpackQualityController>();
            });

            On.RoR2.EquipmentSlot.FireJetpack += EquipmentSlot_FireJetpack;
        }

        static bool EquipmentSlot_FireJetpack(On.RoR2.EquipmentSlot.orig_FireJetpack orig, EquipmentSlot self)
        {
            bool fired = orig(self);

            if (fired)
            {
                JetpackController jetpackController = JetpackController.FindJetpackController(self.gameObject);
                if (jetpackController && jetpackController.TryGetComponent(out JetpackQualityController jetpackQualityController))
                {
                    jetpackQualityController.ActiveQualityTier = self.GetCurrentEquipmentActionQualityTier();
                }
            }

            return fired;
        }
    }
}
