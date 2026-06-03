using HG;
using HG.Coroutines;
using ItemQualities.Utilities.Extensions;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace ItemQualities.ContentManagement
{
    public static class QualityContentManager
    {
        public delegate IEnumerator LoadContentAsyncDelegate(QualityContentLoadArgs args);

        private static event LoadContentAsyncDelegate loadContentInternal;
        public static event LoadContentAsyncDelegate LoadContentAsync
        {
            add
            {
                if (_hasCollectedLoadCoroutines)
                {
                    Log.Error("Cannot add content load callback after content initialization has already started.");
                    return;
                }

                loadContentInternal += value;
            }
            remove
            {
                loadContentInternal -= value;
            }
        }

        private static bool _hasCollectedLoadCoroutines = false;

        [ContentInitializer]
        private static IEnumerator LoadContent(ContentInitializerArgs args)
        {
            if (loadContentInternal == null)
            {
                args.ProgressReceiver.Report(1f);
                yield break;
            }

            PartitionedProgress<ReadableProgress<float>> totalProgress = new PartitionedProgress<ReadableProgress<float>>(args.ProgressReceiver);
            ProgressPartition loadContentProgress = totalProgress.AddPartition();
            ProgressPartition generateAssetsProgress = totalProgress.AddPartition();

            ParallelProgressCoroutine loadContentCoroutine = new ParallelProgressCoroutine(loadContentProgress);

            List<ItemQualityGroup> itemQualityGroups = new List<ItemQualityGroup>();
            List<EquipmentQualityGroup> equipmentQualityGroups = new List<EquipmentQualityGroup>();
            List<BuffQualityGroup> buffQualityGroups = new List<BuffQualityGroup>();

            foreach (LoadContentAsyncDelegate loadContentDelegate in loadContentInternal.GetInvocationList()
                                                                                        .OfType<LoadContentAsyncDelegate>())
            {
                if (loadContentDelegate != null)
                {
                    ReadableProgress<float> progressReceiver = new ReadableProgress<float>();

                    QualityContentLoadArgs loadArgs = new QualityContentLoadArgs(itemQualityGroups, equipmentQualityGroups, buffQualityGroups, progressReceiver);
                    loadContentCoroutine.Add(safeCoroutineWrapper(loadContentDelegate, loadArgs), progressReceiver);
                }
            }

            _hasCollectedLoadCoroutines = true;

            yield return loadContentCoroutine;

            ParallelProgressCoroutine generateAssetsCoroutine = new ParallelProgressCoroutine(generateAssetsProgress);

            foreach (ItemQualityGroup itemGroup in itemQualityGroups)
            {
                generateAssetsCoroutine.AddProgressCoroutine(itemGroup.GenerateRuntimeAssetsAsync, args.ContentPack);
            }

            foreach (EquipmentQualityGroup equipmentGroup in equipmentQualityGroups)
            {
                generateAssetsCoroutine.AddProgressCoroutine(equipmentGroup.GenerateRuntimeAssetsAsync, args.ContentPack);
            }

            foreach (BuffQualityGroup buffGroup in buffQualityGroups)
            {
                generateAssetsCoroutine.AddProgressCoroutine(buffGroup.GenerateRuntimeAssetsAsync, args.ContentPack);
            }

            yield return generateAssetsCoroutine;

            if (itemQualityGroups.Count > 0)
            {
                args.ContentPack.itemQualityGroups.Add(itemQualityGroups.ToArray());
            }

            if (equipmentQualityGroups.Count > 0)
            {
                args.ContentPack.equipmentQualityGroups.Add(equipmentQualityGroups.ToArray());
            }

            if (buffQualityGroups.Count > 0)
            {
                args.ContentPack.buffQualityGroups.Add(buffQualityGroups.ToArray());
            }

            args.ProgressReceiver.Report(1f);
        }

        private static IEnumerator safeCoroutineWrapper(LoadContentAsyncDelegate loadContentDelegate, QualityContentLoadArgs args)
        {
            IEnumerator coroutine;
            try
            {
                coroutine = loadContentDelegate(args);
            }
            catch (Exception e)
            {
                Log.Error_NoCallerPrefix(e.ToString());
                coroutine = null;
            }

            if (coroutine != null)
            {
                while (coroutine.SafeMoveNext())
                {
                    yield return coroutine.Current;
                }
            }

            args.ProgressReceiver.Report(1f);
        }
    }
}
