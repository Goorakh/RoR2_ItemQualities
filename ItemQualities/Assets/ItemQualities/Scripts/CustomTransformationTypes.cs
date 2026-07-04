using HG;
using ItemQualities.ContentManagement;
using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using R2API;
using RoR2;
using RoR2.UI;
using RoR2BepInExPack.GameAssetPathsBetter;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace ItemQualities
{
    public static class CustomTransformationTypes
    {
        const CharacterMasterNotificationQueue.TransformationType StartIndex = (CharacterMasterNotificationQueue.TransformationType)104; // Chosen at random, just needs to be unique
        public const CharacterMasterNotificationQueue.TransformationType QualityUpgradeUncommon = StartIndex + 0;
        public const CharacterMasterNotificationQueue.TransformationType QualityUpgradeRare = StartIndex + 1;
        public const CharacterMasterNotificationQueue.TransformationType QualityUpgradeEpic = StartIndex + 2;
        public const CharacterMasterNotificationQueue.TransformationType QualityUpgradeLegendary = StartIndex + 3;

        static readonly GameObject[] _qualityUpgradeTransformationNotificationPrefabs = new GameObject[(int)QualityTier.Count];

        [ContentInitializer]
        static IEnumerator LoadContent(ContentInitializerArgs args)
        {
            AsyncOperationHandle<GameObject> genericTransformationNotificationPanelLoad = AddressableUtil.LoadTempAssetAsync<GameObject>(RoR2_Base_UI.GenericTransformationNotificationPanel_prefab);
            genericTransformationNotificationPanelLoad.OnSuccess(genericTransformationNotificationPanelPrefab =>
            {
                for (QualityTier qualityTier = 0; qualityTier < QualityTier.Count; qualityTier++)
                {
                    GameObject qualityUpgradeTransformationNotificationPanelPrefab = genericTransformationNotificationPanelPrefab.InstantiateClone("QualityUpgradeTransformationNotificationPanel" + qualityTier.ToString(), false);

                    _qualityUpgradeTransformationNotificationPrefabs[(int)qualityTier] = qualityUpgradeTransformationNotificationPanelPrefab;
                }
            });

            return genericTransformationNotificationPanelLoad.AsProgressCoroutine(args.ProgressReceiver);
        }

        [SystemInitializer(typeof(QualityCatalog))]
        static void Init()
        {
            IL.RoR2.UI.NotificationUIController.SetUpNotification += NotificationUIController_SetUpNotification;

            for (QualityTier qualityTier = 0; qualityTier < QualityTier.Count; qualityTier++)
            {
                GameObject qualityUpgradeTransformationNotificationPanelPrefab = _qualityUpgradeTransformationNotificationPrefabs[(int)qualityTier];
                if (!qualityUpgradeTransformationNotificationPanelPrefab)
                    continue;

                EventFunctions eventFunctions = qualityUpgradeTransformationNotificationPanelPrefab.EnsureComponent<EventFunctions>();

                StartEvent startEvent = qualityUpgradeTransformationNotificationPanelPrefab.AddComponent<StartEvent>();
                startEvent.action ??= new UnityEvent();
                startEvent.action.AddPersistentListener(eventFunctions.PlaySound, QualityCatalog.GetQualityTierDef(qualityTier).pickupDropSound.eventName);
            }
        }

        static void NotificationUIController_SetUpNotification(ILContext il)
        {
            if (!il.Method.TryFindParameter<CharacterMasterNotificationQueue.NotificationInfo>(out ParameterDefinition notificationInfoParameter))
            {
                Log.Error("Failed to find CharacterMasterNotificationQueue.NotificationInfo parameter");
                return;
            }

            ILCursor c = new ILCursor(il);

            VariableDefinition transformationNotificationPrefabVar = null;
            if (!c.TryGotoNext(MoveType.After,
                               x => x.MatchLdfld<NotificationUIController>(nameof(NotificationUIController.genericTransformationNotificationPrefab)),
                               x => x.MatchStloc(il, out transformationNotificationPrefabVar)))
            {
                Log.Error("Failed to find transformationNotificationPrefab variable");
                return;
            }

            c.Goto(0);

            ILLabel afterTransformationTypeSwitchLabel = null;
            if (!c.TryGotoNext(MoveType.After,
                               x => x.MatchLdfld<CharacterMasterNotificationQueue.TransformationInfo>(nameof(CharacterMasterNotificationQueue.TransformationInfo.transformationType))) ||
                !c.TryGotoNext(MoveType.After,
                               x => x.MatchSwitch(out _),
                               x => x.MatchBr(out afterTransformationTypeSwitchLabel)))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Goto(afterTransformationTypeSwitchLabel.Target, MoveType.AfterLabel);

            c.Emit(OpCodes.Ldarg, notificationInfoParameter);
            c.Emit(OpCodes.Ldloca, transformationNotificationPrefabVar);
            c.EmitDelegate<CheckCustomTransformationTypeDelegate>(checkCustomTransformationType);

            static void checkCustomTransformationType(CharacterMasterNotificationQueue.NotificationInfo notificationInfo, ref GameObject transformationNotificationPrefab)
            {
                switch (notificationInfo.transformation.transformationType)
                {
                    case QualityUpgradeUncommon:
                    case QualityUpgradeRare:
                    case QualityUpgradeEpic:
                    case QualityUpgradeLegendary:
                        QualityTier qualityTier = (QualityTier)(notificationInfo.transformation.transformationType - QualityUpgradeUncommon);
                        GameObject qualityUpgradeTransformationNotificationPrefab = ArrayUtils.GetSafe(_qualityUpgradeTransformationNotificationPrefabs, (int)qualityTier);
                        if (qualityUpgradeTransformationNotificationPrefab)
                        {
                            transformationNotificationPrefab = qualityUpgradeTransformationNotificationPrefab;
                        }

                        break;
                }
            }
        }

        delegate void CheckCustomTransformationTypeDelegate(CharacterMasterNotificationQueue.NotificationInfo notificationInfo, ref GameObject transformationNotificationPrefab);
    }
}
