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
        public delegate IEnumerator LoadContentDelegate(QualityContentLoadArgs args);
        public static event LoadContentDelegate LoadContentCallback;

        [ContentInitializer]
        static IEnumerator LoadContent(ContentIntializerArgs args)
        {
            if (LoadContentCallback == null)
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

            foreach (LoadContentDelegate loadContentDelegate in LoadContentCallback.GetInvocationList()
                                                                                   .OfType<LoadContentDelegate>())
            {
                if (loadContentDelegate != null)
                {
                    ReadableProgress<float> progressReceiver = new ReadableProgress<float>();

                    QualityContentLoadArgs loadArgs = new QualityContentLoadArgs(itemQualityGroups, equipmentQualityGroups, buffQualityGroups, progressReceiver);
                    loadContentCoroutine.Add(safeCoroutineWrapper(loadContentDelegate, loadArgs), progressReceiver);
                }
            }

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

        static IEnumerator safeCoroutineWrapper(LoadContentDelegate loadContentDelegate, QualityContentLoadArgs args)
        {
            IEnumerator coroutine;
            try
            {
                coroutine = loadContentDelegate(args);
            }
            catch (Exception e)
            {
                Log.Error_NoCallerPrefix(e);
                yield break;
            }

            if (coroutine == null)
                yield break;

            while (safeMoveNext(coroutine, out object current))
            {
                yield return current;
            }
        }
    }
}
