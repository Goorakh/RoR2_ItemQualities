using System;
using System.Collections;
using System.Collections.Generic;

namespace ItemQualities.ContentManagement
{
    public interface IAsyncAssetGenerator
    {
        IEnumerator GenerateAssetsAsync(ICollection<UnityEngine.Object> dest, IProgress<float> progressReceiver = null);
    }
}
