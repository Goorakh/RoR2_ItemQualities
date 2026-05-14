using HG;
using HG.Coroutines;
using System;
using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace ItemQualities.Utilities.Extensions
{
    internal static class AssetLoadExtensions
    {
        static class AsyncOperationHandleStaticData<T>
        {
            public static readonly PropertyInfo LocationNamePropertyInfo = typeof(AsyncOperationHandle<T>).GetProperty("LocationName", BindingFlags.NonPublic | BindingFlags.Instance);
        }

        public static void OnSuccess<T>(this in AsyncOperationHandle<T> handle, Action<T> onSuccess)
        {
#if DEBUG
            System.Diagnostics.StackTrace stackTrace = new();
#endif

            void handleCompleted(in AsyncOperationHandle<T> handle)
            {
                if (handle.Status != AsyncOperationStatus.Succeeded)
                {
                    string locationName = AsyncOperationHandleStaticData<T>.LocationNamePropertyInfo?.GetValue(handle) as string;
                    if (string.IsNullOrEmpty(locationName))
                    {
                        locationName = handle.DebugName;
                        if (string.IsNullOrEmpty(locationName))
                        {
                            locationName = $"Unknown handle ({typeof(T).FullName})";
                        }
                    }

                    Log.Error($"Failed to load asset from location/handle: '{locationName}'"
#if DEBUG
                        + $". {stackTrace}"
#endif
                        );

                    return;
                }

                onSuccess(handle.Result);
            }

            if (handle.IsDone)
            {
                handleCompleted(handle);
            }
            else
            {
                handle.Completed += onCompleted;

                void onCompleted(AsyncOperationHandle<T> handle)
                {
                    handleCompleted(handle);
                }
            }
        }

        public static IEnumerator AsProgressCoroutine<TProgress>(this AsyncOperation asyncOperation, TProgress progressReceiver)
            where TProgress : IProgress<float>
        {
            while (!asyncOperation.isDone)
            {
                yield return null;
                progressReceiver.Report(asyncOperation.progress);
            }
        }

        public static IEnumerator AsProgressCoroutine<TProgress>(this AsyncOperationHandle asyncOperation, TProgress progressReceiver)
            where TProgress : IProgress<float>
        {
            while (!asyncOperation.IsDone)
            {
                yield return null;
                progressReceiver.Report(asyncOperation.PercentComplete);
            }
        }

        public static IEnumerator AsProgressCoroutine<T, TProgress>(this AsyncOperationHandle<T> asyncOperation, TProgress progressReceiver)
            where TProgress : IProgress<float>
        {
            while (!asyncOperation.IsDone)
            {
                yield return null;
                progressReceiver.Report(asyncOperation.PercentComplete);
            }
        }

        public static void AddProgressCoroutine(this ParallelProgressCoroutine parallelProgressCoroutine, Func<ReadableProgress<float>, IEnumerator> coroutine)
        {
            ReadableProgress<float> progressReceiver = new ReadableProgress<float>();
            parallelProgressCoroutine.Add(coroutine(progressReceiver), progressReceiver);
        }

        public static void AddProgressCoroutine<TArg>(this ParallelProgressCoroutine parallelProgressCoroutine, Func<TArg, ReadableProgress<float>, IEnumerator> coroutine, TArg arg)
        {
            ReadableProgress<float> progressReceiver = new ReadableProgress<float>();
            parallelProgressCoroutine.Add(coroutine(arg, progressReceiver), progressReceiver);
        }

        public static void Add(this ParallelProgressCoroutine parallelProgressCoroutine, AsyncOperation asyncOperation)
        {
            ReadableProgress<float> progressReceiver = new ReadableProgress<float>();
            parallelProgressCoroutine.Add(asyncOperation.AsProgressCoroutine(progressReceiver), progressReceiver);
        }

        public static void Add(this ParallelProgressCoroutine parallelProgressCoroutine, in AsyncOperationHandle asyncOperation)
        {
            ReadableProgress<float> progressReceiver = new ReadableProgress<float>();
            parallelProgressCoroutine.Add(asyncOperation.AsProgressCoroutine(progressReceiver), progressReceiver);
        }

        public static void Add<T>(this ParallelProgressCoroutine parallelProgressCoroutine, in AsyncOperationHandle<T> asyncOperation)
        {
            ReadableProgress<float> progressReceiver = new ReadableProgress<float>();
            parallelProgressCoroutine.Add(asyncOperation.AsProgressCoroutine(progressReceiver), progressReceiver);
        }

        public static bool AssertLoaded(this in AsyncOperationHandle asyncOperation, string assetName = null, [CallerFilePath] string callerPath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
        {
            if (!asyncOperation.IsValid() || asyncOperation.Status != AsyncOperationStatus.Succeeded || asyncOperation.Result == null)
            {
                Log.Error($"Failed to load asset {assetName ?? asyncOperation.DebugName}: {(asyncOperation.IsValid() ? asyncOperation.OperationException : "Invalid Handle")}", callerPath, callerMemberName, callerLineNumber);
                return false;
            }

            return true;
        }

        public static bool AssertLoaded<T>(this in AsyncOperationHandle<T> asyncOperation, string assetName = null, [CallerFilePath] string callerPath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
        {
            if (!asyncOperation.IsValid() || asyncOperation.Status != AsyncOperationStatus.Succeeded || asyncOperation.Result == null)
            {
                Log.Error($"Failed to load asset {assetName ?? asyncOperation.DebugName}: {(asyncOperation.IsValid() ? asyncOperation.OperationException : "Invalid Handle")}", callerPath, callerMemberName, callerLineNumber);
                return false;
            }

            return true;
        }
    }
}
