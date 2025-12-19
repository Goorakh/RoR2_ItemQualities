using HG;
using RoR2;
using RoR2.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace ItemQualities
{
#if DEBUG
    static class InteractableDebugger
    {
        [ConCommand(commandName = "test_interactable_appearences", flags = ConVarFlags.SenderMustBeServer)]
        static void CCStartInteractableDebugging(ConCommandArgs args)
        {
            if (!Run.instance)
            {
                Debug.LogError("Must be in a run");
                return;
            }

            args.CheckArgumentCount(1);

            List<string> interactableNames = new List<string>(args.Count);
            for (int i = 0; i < args.Count; i++)
            {
                string argString = args.TryGetArgString(i);
                if (!string.IsNullOrEmpty(argString))
                {
                    if (string.Equals(argString, "allquality", StringComparison.OrdinalIgnoreCase))
                    {
                        interactableNames.Add("QualityChest1");
                        interactableNames.Add("QualityChest2");
                        interactableNames.Add("QualityEquipmentBarrel");
                        interactableNames.Add("QualityDuplicator");
                        interactableNames.Add("QualityDuplicatorLarge");
                        interactableNames.Add("QualityDuplicatorMilitary");
                        interactableNames.Add("QualityDuplicatorWild");
                    }
                    else
                    {
                        interactableNames.Add(argString);
                    }
                }
            }

            InteractableAppearenceTester appearenceTester = Run.instance.gameObject.AddComponent<InteractableAppearenceTester>();
            appearenceTester.TestInteractableNames = interactableNames.ToArray();
        }

        sealed class InteractableAppearenceTester : MonoBehaviour
        {
            public string[] TestInteractableNames = Array.Empty<string>();

            readonly List<InteractableAppearenceInfo> _interactableAppearences = new List<InteractableAppearenceInfo>();

            readonly HashSet<SceneIndex> _searchedSceneIndices = new HashSet<SceneIndex>();

            int _stageSearchCount;

            bool _scenePopulated;

            void Start()
            {
                StartCoroutine(runTestRoutine());
            }

            void OnEnable()
            {
                SceneDirector.onPostPopulateSceneServer += onPostPopulateSceneServer;

                if (Stage.instance)
                {
                    _scenePopulated = true;
                }
            }

            void OnDisable()
            {
                SceneDirector.onPostPopulateSceneServer -= onPostPopulateSceneServer;

                StringBuilder resultBuilder = new StringBuilder();

                resultBuilder.Append($"Searched {_stageSearchCount} stage(s):");

                foreach (string interactableName in TestInteractableNames)
                {
                    List<InteractableAppearenceInfo> appearenceInfos = _interactableAppearences.FindAll(appearence => appearence.InteractableName == interactableName);

                    int totalAppearenceCount = 0;

                    int[] interactableCountAppearences = Array.Empty<int>();
                    int[] appearencesBySceneIndex = new int[SceneCatalog.sceneDefCount];

                    foreach (InteractableAppearenceInfo appearenceInfo in appearenceInfos)
                    {
                        totalAppearenceCount += appearenceInfo.InteractableCount;

                        ArrayUtils.EnsureCapacity(ref interactableCountAppearences, appearenceInfo.InteractableCount);
                        interactableCountAppearences[appearenceInfo.InteractableCount - 1]++;

                        appearencesBySceneIndex[(int)appearenceInfo.SceneIndex] += appearenceInfo.InteractableCount;
                    }

                    StringBuilder sb = new StringBuilder();

                    sb.AppendLine();
                    sb.AppendLine($"{interactableName}:");

                    sb.Append(' ', 4).AppendLine($"Total: {totalAppearenceCount}");
                    sb.Append(' ', 4).AppendLine($"Avg. per stage: {totalAppearenceCount / (float)_stageSearchCount}");
                    sb.Append(' ', 4).AppendLine($"Avg. per loop: {totalAppearenceCount / (_stageSearchCount / 5f)}");

                    if (interactableCountAppearences.Length > 0)
                    {
                        sb.AppendLine();

                        for (int interactableCount = 0; interactableCount < interactableCountAppearences.Length; interactableCount++)
                        {
                            int appearences = interactableCountAppearences[interactableCount];

                            sb.Append(' ', 4).AppendLine($"{interactableCount + 1} appeared {appearences} time(s)");
                        }
                    }

                    if (totalAppearenceCount > 0)
                    {
                        sb.AppendLine();

                        sb.Append(' ', 4).AppendLine("Appears on:");
                        for (SceneIndex sceneIndex = 0; (int)sceneIndex < appearencesBySceneIndex.Length; sceneIndex++)
                        {
                            SceneDef sceneDef = SceneCatalog.GetSceneDef(sceneIndex);
                            if (!sceneDef)
                                continue;

                            int sceneAppearences = appearencesBySceneIndex[(int)sceneIndex];
                            if (sceneAppearences > 0)
                            {
                                sb.Append(' ', 4 + 4).AppendLine($"{Language.GetString(sceneDef.nameToken)} ({sceneDef.cachedName}): {sceneAppearences / (float)totalAppearenceCount:P}");
                            }
                        }
                    }

                    SceneIndex[] scenesWithNoAppearances = _searchedSceneIndices.Where(sceneIndex => appearencesBySceneIndex[(int)sceneIndex] == 0).ToArray();
                    if (scenesWithNoAppearances.Length > 0)
                    {
                        sb.AppendLine();

                        sb.Append(' ', 4).AppendLine("Never appears on:");

                        foreach (SceneIndex sceneIndex in scenesWithNoAppearances)
                        {
                            SceneDef sceneDef = SceneCatalog.GetSceneDef(sceneIndex);
                            if (sceneDef)
                            {
                                sb.Append(' ', 4 * 2).AppendLine($"{Language.GetString(sceneDef.nameToken)} ({sceneDef.cachedName})");
                            }
                        }
                    }

                    resultBuilder.Append(sb);
                }

                Log.Info(resultBuilder);
            }

            void onPostPopulateSceneServer(SceneDirector sceneDirector)
            {
                _scenePopulated = true;
            }

            IEnumerator runTestRoutine()
            {
                while (_stageSearchCount < 100)
                {
                    yield return new WaitUntil(() => _scenePopulated);
                    _scenePopulated = false;

                    SceneIndex currentSceneIndex = SceneCatalog.mostRecentSceneDef.sceneDefIndex;
                    _searchedSceneIndices.Add(currentSceneIndex);

                    Dictionary<string, InteractableAppearenceInfo> appearenceInfoByInteractableName = new Dictionary<string, InteractableAppearenceInfo>(TestInteractableNames.Length);
                    
                    foreach (PurchaseInteraction purchaseInteraction in InstanceTracker.GetInstancesList<PurchaseInteraction>())
                    {
                        string interactableObjectName = purchaseInteraction.name;
                        string interactableName = Array.Find(TestInteractableNames, testInteractableName =>
                        {
                            string objectName = interactableObjectName;
                            if (objectName.EndsWith("(Clone)"))
                                objectName = objectName.Remove(objectName.Length - "(Clone)".Length);

                            return objectName == testInteractableName;
                        });

                        if (!string.IsNullOrEmpty(interactableName))
                        {
                            if (!appearenceInfoByInteractableName.TryGetValue(interactableName, out InteractableAppearenceInfo appearenceInfo))
                            {
                                appearenceInfo = new InteractableAppearenceInfo(interactableName, 0, currentSceneIndex);
                                appearenceInfoByInteractableName.Add(interactableName, appearenceInfo);
                            }

                            appearenceInfo.InteractableCount++;
                        }
                    }

                    _stageSearchCount++;
                    _interactableAppearences.AddRange(appearenceInfoByInteractableName.Values);

                    yield return new WaitForSecondsRealtime(0.1f);
                    yield return new WaitWhile(() => PauseScreenController.instancesList.Count > 0);

                    Log.Info($"Searched stage {_stageSearchCount} / 100");

                    RoR2.Console.instance.SubmitCmd(LocalUserManager.GetFirstLocalUser(), "next_stage");
                }

                Destroy(this);
            }

            sealed class InteractableAppearenceInfo
            {
                public string InteractableName;
                public int InteractableCount;
                public SceneIndex SceneIndex = SceneIndex.Invalid;

                public InteractableAppearenceInfo(string interactableName, int interactableCount, SceneIndex sceneIndex)
                {
                    InteractableName = interactableName;
                    InteractableCount = interactableCount;
                    SceneIndex = sceneIndex;
                }
            }
        }
    }
#endif
}
