using System;
using System.Collections;

namespace ItemQualities.ContentManagement
{
    internal interface IAsyncContentLoadCallback
    {
        IEnumerator OnContentLoad(IProgress<float> progressReceiver = null);
    }
}
