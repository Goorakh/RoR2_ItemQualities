using System;
using System.Collections;

namespace ItemQualities.ContentManagement
{
    internal interface IAsyncContentLoadCallback
    {
        IEnumerator OnContentLoad<TProgress>(TProgress progressReceiver = default)
            where TProgress : IProgress<float>;
    }
}
