using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using RoR2;
using RoR2.ContentManagement;
using RoR2.Skills;
using RoR2BepInExPack.GameAssetPathsBetter;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace ItemQualities
{
    [CreateAssetMenu(menuName = "ItemQualities/Skills/ParryProjectileSkillDef")]
    public sealed class ParryProjectileSkillDef : SkillDef
    {
        private static readonly AssetReferenceSprite _fallbackSpriteReference = new AssetReferenceSprite(RoR2_Base_Common_MiscIcons.texMysteryIcon_png);

        private sealed class InstanceData : BaseSkillInstanceData
        {
            public CharacterMasterExtraStatsTracker MasterStats;
            public AsyncOperationHandle<Sprite> FallbackSpriteLoadHandle;
        }

        public override BaseSkillInstanceData OnAssigned(GenericSkill skillSlot)
        {
            CharacterBody body = skillSlot.characterBody;
            CharacterMaster master = body ? body.master : null;

            return new InstanceData
            {
                MasterStats = master ? master.GetComponentCached<CharacterMasterExtraStatsTracker>() : null,
                FallbackSpriteLoadHandle = AssetAsyncReferenceManager<Sprite>.LoadAsset(_fallbackSpriteReference, AsyncReferenceHandleUnloadType.OnSceneUnload),
            };
        }

        public override void OnUnassigned(GenericSkill skillSlot)
        {
            AssetAsyncReferenceManager<Sprite>.UnloadAsset(_fallbackSpriteReference);
        }

        public override Sprite GetCurrentIcon(GenericSkill skillSlot)
        {
            InstanceData instanceData = (InstanceData)skillSlot.skillInstanceData;

            ParryStoredProjectileInfo projectileInfo = instanceData.MasterStats ? instanceData.MasterStats.ParryStoredProjectileInfo : ParryStoredProjectileInfo.None;

            Sprite parriedBodyIcon = BodyPortraitIconSpriteCache.GetBodyIconSprite(projectileInfo.AttackerBodyIndex);
            if (parriedBodyIcon)
            {
                return parriedBodyIcon;
            }

            return instanceData.FallbackSpriteLoadHandle.WaitForCompletion();
        }

        public override string GetCurrentNameToken(GenericSkill skillSlot)
        {
            InstanceData instanceData = (InstanceData)skillSlot.skillInstanceData;

            string parriedBodyNameToken = string.Empty;
            if (instanceData.MasterStats && instanceData.MasterStats.ParryStoredProjectileInfo.AttackerBodyIndex != BodyIndex.None)
            {
                CharacterBody parriedBodyPrefab = BodyCatalog.GetBodyPrefabBodyComponent(instanceData.MasterStats.ParryStoredProjectileInfo.AttackerBodyIndex);
                if (parriedBodyPrefab && !string.IsNullOrWhiteSpace(parriedBodyPrefab.baseNameToken))
                {
                    parriedBodyNameToken = parriedBodyPrefab.baseNameToken;
                }
            }

            return parriedBodyNameToken;
        }

        public override string GetCurrentDescriptionToken(GenericSkill skillSlot)
        {
            InstanceData instanceData = (InstanceData)skillSlot.skillInstanceData;

            string parriedBodySubtitleToken = string.Empty;
            if (instanceData.MasterStats && instanceData.MasterStats.ParryStoredProjectileInfo.AttackerBodyIndex != BodyIndex.None)
            {
                CharacterBody parriedBodyPrefab = BodyCatalog.GetBodyPrefabBodyComponent(instanceData.MasterStats.ParryStoredProjectileInfo.AttackerBodyIndex);
                if (parriedBodyPrefab && !string.IsNullOrWhiteSpace(parriedBodyPrefab.subtitleNameToken))
                {
                    parriedBodySubtitleToken = parriedBodyPrefab.subtitleNameToken;
                }
            }

            return parriedBodySubtitleToken;
        }
    }
}
