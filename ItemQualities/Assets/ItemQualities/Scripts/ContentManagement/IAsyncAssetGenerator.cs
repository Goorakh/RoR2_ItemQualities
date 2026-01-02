using System;
using System.Collections;

namespace ItemQualities.ContentManagement
{
    internal interface IAsyncAssetGenerator
    {
        IEnumerator GenerateAssetsAsync(ExtendedContentPack contentPack, IProgress<float> progressReceiver = null);
    }
}
