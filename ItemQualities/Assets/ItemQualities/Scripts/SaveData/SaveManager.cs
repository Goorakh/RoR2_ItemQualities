using ItemQualities.ModCompatibility;
using ItemQualities.Serialization;
using ItemQualities.Utilities.Extensions;
using RoR2;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;

namespace ItemQualities.SaveData
{
    internal static class SaveManager
    {
        /// <summary>
        /// The current version of the binary save file format, increment whenever anything about the serialization changes. Retrieve the value from <see cref="DeserializerContext.SerializedVersion"/> when deserializing.
        /// </summary>
        public const uint SaveFileVersion = 1;

        private static SaveContainerBreadBox _saveContainerBreadBox;
        public static SaveContainer LoadedSaveData
        {
            get => ProperSaveCompat.Enabled ? _saveContainerBreadBox.Value : null;
            private set => _saveContainerBreadBox.Value = value;
        }

        [InitDuringStartupPhase(GameInitPhase.PostProgressBar)]
        private static void TryInit()
        {
            if (ProperSaveCompat.Enabled)
            {
                Init();
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private static void Init()
        {
            _saveContainerBreadBox = new SaveContainerBreadBox();

            ProperSave.SaveFile.OnGatherSaveData += onGatherSaveData;

            ProperSave.Loading.OnLoadingStarted += onLoadingStarted;
            SpawnUtils.PreSceneReadyForSpawnsServer += preSceneReadyForSpawnsServer;
        }

        private static void onLoadingStarted(ProperSave.SaveFile saveFile)
        {
            LoadedSaveData = null;

            if (saveFile == null || !saveFile.ModdedData.ContainsKey(ItemQualitiesPlugin.PluginGUID))
            {
                return;
            }

            try
            {
                string saveBytesB64 = saveFile.GetModdedData<string>(ItemQualitiesPlugin.PluginGUID);
                byte[] saveBytes = Convert.FromBase64String(saveBytesB64);

                using (MemoryStream stream = new MemoryStream(saveBytes))
                using (DeserializerContext context = new DeserializerContext(stream))
                {
                    Log.Debug($"Reading save file v{context.SerializedVersion}, shared bits: {context.SharedBitCount}");

                    LoadedSaveData = context.Read<SaveContainer>();
                }
            }
            catch (Exception e)
            {
                Log.Error_NoCallerPrefix(e.ToString());
            }
        }

        private static void preSceneReadyForSpawnsServer(SceneDirector sceneDirector)
        {
            if (LoadedSaveData == null)
            {
                return;
            }

            foreach (MasterSaveData masterSaveData in LoadedSaveData.Masters)
            {
                CharacterMaster master = masterSaveData.Identifier.ResolveMaster();
                if (master && master.TryGetComponentCached(out CharacterMasterExtraStatsTracker masterExtraStats))
                {
                    Log.Debug($"Applying save data for master {Util.GetBestMasterName(master)}");
                    masterExtraStats.InitializeFromSaveServer(masterSaveData);
                }
            }
        }

        private static void onGatherSaveData(Dictionary<string, object> saveDataDict)
        {
            try
            {
                if (saveDataDict.ContainsKey(ItemQualitiesPlugin.PluginGUID))
                {
                    Log.Error("Plugin guid is already present in save data dictionary, existing entry will be overridden");
                }

                List<MasterSaveData> masters = new List<MasterSaveData>(CharacterMaster.readOnlyInstancesList.Count);

                foreach (PlayerCharacterMasterController playerMaster in PlayerCharacterMasterController.instances)
                {
                    masters.Add(new MasterSaveData(playerMaster.master));

                    MinionOwnership.MinionGroup minionGroup = MinionOwnership.MinionGroup.FindGroup(playerMaster.master.netId);
                    if (minionGroup != null)
                    {
                        for (int i = 0; i < minionGroup.memberCount; i++)
                        {
                            MinionOwnership minion = minionGroup.members[i];
                            if (minion && minion.TryGetComponent(out CharacterMaster minionMaster))
                            {
                                masters.Add(new MasterSaveData(minionMaster));
                            }
                        }
                    }
                }

                Log.Debug($"Collected save data from {masters.Count} player & minion master(s)");

                SaveContainer saveContainer = new SaveContainer
                {
                    Masters = masters.ToArray()
                };

                byte[] saveBytes;
                using (MemoryStream memoryStream = new MemoryStream())
                {
                    using (SerializerContext context = new SerializerContext())
                    {
                        saveContainer.Serialize(context);
                        context.WriteTo(memoryStream);
                    }

                    saveBytes = memoryStream.ToArray();
                }

                saveDataDict[ItemQualitiesPlugin.PluginGUID] = Convert.ToBase64String(saveBytes, Base64FormattingOptions.None);
            }
            catch (Exception e)
            {
                Log.Error_NoCallerPrefix(e.ToString());
            }
        }

        /// <summary>
        /// Holds a reference to a save container, with checks to ensure the data is not stale when retrieved
        /// </summary>
        private sealed class SaveContainerBreadBox
        {
            // Stored as an object to prevent the runtime from attempting to resolve the type when initializing
            private object _loadedFromProperSaveFile;

            private SaveContainer _value;
            public SaveContainer Value
            {
                [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
                get
                {
                    if (_loadedFromProperSaveFile != null && !ReferenceEquals(_loadedFromProperSaveFile, ProperSave.Loading.CurrentSave))
                    {
                        _value = null;
                        _loadedFromProperSaveFile = null;
                    }

                    return _value;
                }
                [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
                set
                {
                    _value = value;
                    _loadedFromProperSaveFile = _value != null ? ProperSave.Loading.CurrentSave : null;
                }
            }
        }
    }
}
