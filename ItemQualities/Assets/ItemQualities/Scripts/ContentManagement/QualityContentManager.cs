using HG;
using HG.Coroutines;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace ItemQualities.ContentManagement
{
    public static class QualityContentManager
    {
        public delegate IEnumerator LoadContentAsyncDelegate(QualityContentLoadArgs args);

        static event LoadContentAsyncDelegate loadContentInternal;
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

        static bool _hasCollectedLoadCoroutines = false;

        [ContentInitializer]
        static IEnumerator LoadContent(ContentIntializerArgs args)
        {
            if (loadContentInternal == null)
            {
                args.ProgressReceiver.Report(1f);
                yield break;
            }

            using PartitionedProgress totalProgress = new PartitionedProgress(args.ProgressReceiver);
            IProgress<float> loadContentProgress = totalProgress.AddPartition();
            IProgress<float> generateAssetsProgress = totalProgress.AddPartition();

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
                ReadableProgress<float> progressReceiver = new ReadableProgress<float>();
                generateAssetsCoroutine.Add(itemGroup.GenerateRuntimeAssetsAsync(args.ContentPack, progressReceiver), progressReceiver);
            }

            foreach (EquipmentQualityGroup equipmentGroup in equipmentQualityGroups)
            {
                ReadableProgress<float> progressReceiver = new ReadableProgress<float>();
                generateAssetsCoroutine.Add(equipmentGroup.GenerateRuntimeAssetsAsync(args.ContentPack, progressReceiver), progressReceiver);
            }

            foreach (BuffQualityGroup buffGroup in buffQualityGroups)
            {
                ReadableProgress<float> progressReceiver = new ReadableProgress<float>();
                generateAssetsCoroutine.Add(buffGroup.GenerateRuntimeAssetsAsync(args.ContentPack, progressReceiver), progressReceiver);
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

        static bool safeMoveNext(IEnumerator enumerator, out object current)
        {
            try
            {
                if (enumerator.MoveNext())
                {
                    current = enumerator.Current;
                    return true;
                }
            }
            catch (Exception e)
            {
                Log.Error_NoCallerPrefix(e);
            }

            current = null;
            return false;
        }

        static IEnumerator safeCoroutineWrapper(LoadContentAsyncDelegate loadContentDelegate, QualityContentLoadArgs args)
        {
            IEnumerator coroutine;
            try
            {
                coroutine = loadContentDelegate(args);
            }
            catch (Exception e)
            {
                Log.Error_NoCallerPrefix(e);
                coroutine = null;
            }

            if (coroutine != null)
            {
                while (safeMoveNext(coroutine, out object current))
                {
                    yield return current;
                }
            }

            args.ProgressReceiver.Report(1f);
        }
    }
}
