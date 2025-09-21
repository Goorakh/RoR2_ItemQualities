using System;
using System.Collections;

namespace ItemQualities.ContentManagement
{
    public interface IAsyncContentLoadCallback
    {
        IEnumerator OnContentLoad(IProgress<float> progressReceiver = null);
    }
}
