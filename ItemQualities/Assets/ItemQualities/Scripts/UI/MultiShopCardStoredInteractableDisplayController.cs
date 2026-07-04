using ItemQualities.Utilities.Extensions;
using RoR2;
using RoR2.UI;
using RoR2BepInExPack.GameAssetPaths.Version_1_39_0;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;

namespace ItemQualities.UI
{
    [RequireComponent(typeof(TooltipContext))]
    public sealed class MultiShopCardStoredInteractableDisplayController : MonoBehaviour
    {
        public RawImage InteractableIcon;

        public LanguageTextMeshController InteractableNameLabel;

        public GameObject DisplayRoot;

        TooltipContext _context;

        void Awake()
        {
            _context = GetComponent<TooltipContext>();
        }

        void OnEnable()
        {
            _context.OnTooltipProviderDiscovered += rebuild;
            rebuild();
        }

        void OnDisable()
        {
            _context.OnTooltipProviderDiscovered -= rebuild;
        }

        void rebuild()
        {
            EquipmentIcon equipmentIcon = _context.SourceTooltipProvider ? _context.SourceTooltipProvider.GetComponent<EquipmentIcon>() : null;
            Inventory inventory = equipmentIcon ? equipmentIcon.targetInventory : null;
            CharacterMasterExtraStatsTracker masterStats = inventory ? inventory.GetComponentCached<CharacterMasterExtraStatsTracker>() : null;

            StoredInteractableInfo storedInteractableInfo = masterStats ? masterStats.CardStoredInteractableInfo : StoredInteractableInfo.None;

            int storedInteractableIndex = storedInteractableInfo.InteractableIndex;
            int storedInteractableUpgradeValue = storedInteractableInfo.UpgradeValue;

            if (storedInteractableIndex != -1)
            {
                InteractableDef interactableDef = InteractableCatalog.GetInteractableDef(storedInteractableIndex);

                string interactableNameToken = string.Empty;
                Texture interactableIcon = null;

                bool hasName = false;
                bool hasIcon = false;

                if ((!hasName || !hasIcon) && interactableDef.PrefabSpecialObjectAttributes)
                {
                    if (!hasName && !string.IsNullOrWhiteSpace(interactableDef.PrefabSpecialObjectAttributes.bestName))
                    {
                        interactableNameToken = interactableDef.PrefabSpecialObjectAttributes.bestName;
                        hasName = true;
                    }

                    if (!hasIcon && interactableDef.PrefabSpecialObjectAttributes.portraitIcon)
                    {
                        interactableIcon = interactableDef.PrefabSpecialObjectAttributes.portraitIcon;
                        hasIcon = true;
                    }
                }

                IInspectInfoProvider inspectInfoProvider = interactableDef.PrefabInspectInfoProvider;
                if (inspectInfoProvider is ShopTerminalBehavior)
                {
                    inspectInfoProvider = interactableDef.PrefabGenericInspectInfoProvider;
                }

                if ((!hasName || !hasIcon) && inspectInfoProvider != null)
                {
                    InspectInfo inspectInfo = inspectInfoProvider.GetInfo();
                    if (inspectInfo != null)
                    {
                        if (!hasName && !string.IsNullOrWhiteSpace(inspectInfo.TitleToken))
                        {
                            interactableNameToken = inspectInfo.TitleToken;
                            hasName = true;
                        }

                        if (!hasIcon && inspectInfo.Visual && inspectInfo.Visual.texture)
                        {
                            interactableIcon = inspectInfo.Visual.texture;
                            hasIcon = true;
                        }
                    }
                }

                if (!hasName && interactableDef.PrefabDisplayNameProvider != null)
                {
                    string displayName = interactableDef.PrefabDisplayNameProvider.GetDisplayName();
                    if (!string.IsNullOrWhiteSpace(displayName))
                    {
                        interactableNameToken = displayName;
                        hasName = true;
                    }
                }

                if (!hasIcon && interactableDef.PrefabPingInfoProvider)
                {
                    Sprite pingIconOverride = interactableDef.PrefabPingInfoProvider.pingIconOverride;
                    if (pingIconOverride && pingIconOverride.texture)
                    {
                        interactableIcon = pingIconOverride.texture;
                    }
                }

                if (!hasName)
                {
                    interactableNameToken = interactableDef.Name;
                    hasName = true;
                }

                if (storedInteractableUpgradeValue > 0)
                {
                    interactableNameToken = Util.GetNameFromUpgradeCount(interactableNameToken, storedInteractableUpgradeValue);
                }

                if (!hasIcon)
                {
                    interactableIcon = Addressables.LoadAssetAsync<Texture2D>(RoR2_Base_Common_MiscIcons.texMysteryIcon_png).WaitForCompletion();
                    hasIcon = true;
                }

                InteractableNameLabel.token = interactableNameToken;
                InteractableIcon.texture = interactableIcon;
            }

            DisplayRoot.SetActive(storedInteractableIndex != -1);
        }
    }
}
