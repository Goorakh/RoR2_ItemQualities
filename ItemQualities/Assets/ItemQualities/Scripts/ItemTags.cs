using R2API;
using RoR2;
using RoR2BepInExPack.GameAssetPaths.Version_1_39_0;
using UnityEngine.AddressableAssets;

namespace ItemQualities
{
    public static class ItemTags
    {
        public static ItemTag MissileRelated { get; private set; } = (ItemTag)(-1);
        public static ItemTag BleedRelated { get; private set; } = (ItemTag)(-1);

        [InitDuringStartupPhase(GameInitPhase.PreFrame)]
        static void PreInit()
        {
            MissileRelated = ItemAPI.AddItemTag("Quality_MissileRelated");
            BleedRelated = ItemAPI.AddItemTag("Quality_BleedRelated");

            static void addTags(string itemDefAssetGuid, params ItemTag[] tags)
            {
                if (tags.Length == 0)
                {
                    Log.Warning($"Empty tags array for item guid '{itemDefAssetGuid}'");
                }

                ItemDef itemDef = Addressables.LoadAssetAsync<ItemDef>(itemDefAssetGuid).WaitForCompletion();

                foreach (ItemTag tag in tags)
                {
                    ItemAPI.ApplyTagToItem(tag, itemDef);
                }
            }

            addTags(RoR2_Base_Missile.Missile_asset, MissileRelated);
            addTags(RoR2_Base_Firework.Firework_asset, MissileRelated);

            addTags(RoR2_DLC1_MissileVoid.MissileVoid_asset, MissileRelated);
            addTags(RoR2_DLC1_MoreMissile.MoreMissile_asset, MissileRelated);

            addTags(RoR2_DLC2_Items_BarrageOnBoss.BarrageOnBoss_asset, MissileRelated);

            addTags(RoR2_Base_BleedOnHit.BleedOnHit_asset, BleedRelated);
            addTags(RoR2_DLC2_Items_TriggerEnemyDebuffs.TriggerEnemyDebuffs_asset, BleedRelated);
            addTags(RoR2_DLC2_Items_TeleportOnLowHealth.TeleportOnLowHealth_asset, BleedRelated);
            addTags(RoR2_Base_BleedOnHitAndExplode.BleedOnHitAndExplode_asset, BleedRelated);
        }
    }
}
