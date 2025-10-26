using BepInEx.Configuration;
using ItemQualities.Utilities;
using RiskOfOptions;
using System;
using System.IO;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace ItemQualities
{
    public static partial class Configs
    {
        const string ModGuid = ItemQualitiesPlugin.PluginGUID;
        const string ModName = ItemQualitiesPlugin.PluginName;

        static bool _attemptedLoadIcon;
        static Sprite _cachedModIconSprite;
        public static Sprite ModIconSprite
        {
            get
            {
                if (!_attemptedLoadIcon)
                {
                    _attemptedLoadIcon = true;

                    try
                    {
                        _cachedModIconSprite = generateIconSprite();
                    }
                    catch (Exception e)
                    {
                        Log.Error_NoCallerPrefix($"Failed to generate icon sprite: {e}");
                    }
                }

                return _cachedModIconSprite;
            }
        }

        internal static void Init(ConfigFile configFile)
        {
#if DEBUG
            Debug.Init(configFile);
#endif
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        internal static void InitRiskOfOptions()
        {
            // Move this out of the debug block when non debug sections are added
#if DEBUG
            ModSettingsManager.SetModDescription("Settings for ItemQualities", ModGuid, ModName);

            Sprite iconSprite = ModIconSprite;
            if (iconSprite)
            {
                ModSettingsManager.SetModIcon(iconSprite, ModGuid, ModName);
            }

            Debug.InitRiskOfOptions();
#endif
        }

        static Sprite generateIconSprite()
        {
            DirectoryInfo containingDirectory = new DirectoryInfo(Path.GetDirectoryName(ItemQualitiesPlugin.Instance.Info.Location));
            FileInfo iconFile = FileUtils.SearchUpwards(containingDirectory, new DirectoryInfo(BepInEx.Paths.PluginPath), "icon.png");

            if (iconFile == null || !iconFile.Exists)
            {
                Log.Warning($"Failed to find icon file from path: {containingDirectory.FullName}");
                return null;
            }

            using FileStream fileStream = iconFile.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
            Span<byte> fileBytes = stackalloc byte[(int)fileStream.Length];
            int bytesRead = fileStream.Read(fileBytes);
            if (bytesRead != fileBytes.Length)
            {
                Log.Error($"Failed to read icon file at {iconFile.FullName}: {bytesRead} byte(s) read, {fileBytes.Length} expected");
                return null;
            }

            Texture2D iconTexture = new Texture2D(256, 256)
            {
                name = $"tex{ItemQualitiesPlugin.PluginName}Icon"
            };

            if (!iconTexture.LoadImage(fileBytes.ToArray()))
            {
                Texture2D.Destroy(iconTexture);
                Log.Error("Failed to load icon into texture");
                return null;
            }

            Sprite iconSprite = Sprite.Create(iconTexture, new Rect(0f, 0f, iconTexture.width, iconTexture.height), new Vector2(0.5f, 0.5f));
            iconSprite.name = iconTexture.name;
            return iconSprite;
        }
    }
}
