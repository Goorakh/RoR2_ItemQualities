using System;
using System.Collections;

namespace ItemQualities.ContentManagement
{
    public interface IAsyncAssetGenerator
    {
        IEnumerator GenerateAssetsAsync(ExtendedContentPack contentPack, IProgress<float> progressReceiver = null);
    }
}
