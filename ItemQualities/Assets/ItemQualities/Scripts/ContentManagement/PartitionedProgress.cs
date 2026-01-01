using System;
using System.Collections.Generic;

namespace ItemQualities.ContentManagement
{
    public sealed class PartitionedProgress : IDisposable
    {
        readonly IProgress<float> _progressReceiver;

        readonly List<ProgressPartition> _partitions = new List<ProgressPartition>();

        public float Progress { get; private set; }

        public PartitionedProgress(IProgress<float> receiver)
        {
            _progressReceiver = receiver;
        }

        public void Dispose()
        {
            foreach (ProgressPartition partition in _partitions)
            {
                partition.OnReport -= onPartitionReport;
            }

            _partitions.Clear();
        }

        public IProgress<float> AddPartition(float weight = 1f)
        {
            ProgressPartition partition = new ProgressPartition(weight);
            partition.OnReport += onPartitionReport;
            _partitions.Add(partition);
            return partition;
        }

        public IProgress<float>[] AddPartitions(int count, float weight = 1f)
        {
            IProgress<float>[] partitions = new IProgress<float>[count];
            for (int i = 0; i < count; i++)
            {
                partitions[i] = AddPartition(weight);
            }

            return partitions;
        }

        void recalculateProgress()
        {
            float progress = 0f;

            if (_partitions.Count > 0)
            {
                float totalPartitionsWeight = 0f;

                foreach (ProgressPartition partition in _partitions)
                {
                    progress += partition.Value * partition.Weight;

                    totalPartitionsWeight += partition.Weight;
                }

                progress /= totalPartitionsWeight;
            }

            Progress = progress;
        }

        void onPartitionReport(float _)
        {
            recalculateProgress();
            _progressReceiver.Report(Progress);
        }

        sealed class ProgressPartition : IProgress<float>
        {
            public event Action<float> OnReport;

            public float Value { get; private set; }

            public float Weight { get; }

            public ProgressPartition(float weight = 1f)
            {
                Weight = weight;
            }

            public void Report(float value)
            {
                Value = value;
                OnReport?.Invoke(value);
            }
        }
    }
}
