using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using RoR2;
using RoR2BepInExPack.GameAssetPathsBetter;
using System.Collections.Generic;

namespace ItemQualities.Items
{
    public class DeathMarkQualityItemBehavior : QualityItemBodyBehavior
    {
        [ItemGroupAssociation(QualityItemBehaviorUsageFlags.Server)]
        private static ItemQualityGroup GetItemGroup() => ItemQualitiesContent.ItemQualityGroups.DeathMark;

        private static WwiseBankReference _soulExplosionReference;

        private AkBank _soulExplosionBank;

        [SystemInitializer]
        private static void Init()
        {
            AddressableUtil.LoadAssetAsync<WwiseBankReference>(Wwise._12364C84_F36A_4AB9_8588_1A2AF5386BE0_asset).OnSuccess(railgunnerSoundReference =>
            {
                _soulExplosionReference = railgunnerSoundReference;
            });
        }

        private void OnEnable()
        {
            _soulExplosionBank = gameObject.AddComponent<AkBank>();
            _soulExplosionBank.data.WwiseObjectReference = _soulExplosionReference;
            _soulExplosionBank.triggerList = new List<int> { AkTriggerHandler.ON_ENABLE_TRIGGER_ID };
            _soulExplosionBank.unloadTriggerList = new List<int> { AkTriggerHandler.ON_DISABLE_TRIGGER_ID };
            _soulExplosionBank.HandleEvent(null);
        }

        private void OnDisable()
        {
            Destroy(_soulExplosionBank);
        }
    }
}
