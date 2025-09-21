using HG;
using HG.Coroutines;
using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace ItemQualities.Utilities.Extensions
{
    public static class AssetLoadExtensions
    {
        public static void OnSuccess<T>(this AsyncOperationHandle<T> handle, Action<T> onSuccess)
        {
#if DEBUG
            System.Diagnostics.StackTrace stackTrace = new();
#endif

            void onCompleted(AsyncOperationHandle<T> handle)
            {
                if (handle.Status != AsyncOperationStatus.Succeeded)
                {
                    string locationName = "???";

                    PropertyInfo locationNameProp = handle.GetType().GetProperty("LocationName", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (locationNameProp != null)
                    {
                        try
                        {
                            locationName = (string)locationNameProp.GetValue(handle);
                        }
                        catch (Exception e)
                        {
                            Log.Error_NoCallerPrefix($"Failed to get value of LocationName property: {e}");
                        }
                    }

                    Log.Error($"Failed to load asset '{locationName}'"
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
                onCompleted(handle);
                return;
            }

            handle.Completed += onCompleted;
        }

        public static IEnumerator AsProgressCoroutine(this AsyncOperation asyncOperation, IProgress<float> progressReceiver)
        {
            while (!asyncOperation.isDone)
            {
                yield return null;
                progressReceiver.Report(asyncOperation.progress);
            }
        }

        public static IEnumerator AsProgressCoroutine(this AsyncOperationHandle asyncOperation, IProgress<float> progressReceiver)
        {
            while (!asyncOperation.IsDone)
            {
                yield return null;
                progressReceiver.Report(asyncOperation.PercentComplete);
            }
        }

        public static IEnumerator AsProgressCoroutine<T>(this AsyncOperationHandle<T> asyncOperation, IProgress<float> progressReceiver)
        {
            while (!asyncOperation.IsDone)
            {
                yield return null;
                progressReceiver.Report(asyncOperation.PercentComplete);
            }
        }

        public static void Add(this ParallelProgressCoroutine parallelProgressCoroutine, AsyncOperation asyncOperation)
        {
            ReadableProgress<float> progressReceiver = new ReadableProgress<float>();
            parallelProgressCoroutine.Add(asyncOperation.AsProgressCoroutine(progressReceiver), progressReceiver);
        }

        public static void Add(this ParallelProgressCoroutine parallelProgressCoroutine, AsyncOperationHandle asyncOperation)
        {
            ReadableProgress<float> progressReceiver = new ReadableProgress<float>();
            parallelProgressCoroutine.Add(asyncOperation.AsProgressCoroutine(progressReceiver), progressReceiver);
        }

        public static void Add<T>(this ParallelProgressCoroutine parallelProgressCoroutine, AsyncOperationHandle<T> asyncOperation)
        {
            ReadableProgress<float> progressReceiver = new ReadableProgress<float>();
            parallelProgressCoroutine.Add(asyncOperation.AsProgressCoroutine(progressReceiver), progressReceiver);
        }
    }
}
