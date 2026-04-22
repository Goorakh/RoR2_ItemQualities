using System;
using System.Collections;

namespace ItemQualities.ContentManagement
{
    internal interface IAsyncAssetGenerator
    {
        IEnumerator GenerateAssetsAsync<TProgress>(ExtendedContentPack contentPack, TProgress progressReceiver = default)
            where TProgress : IProgress<float>;
    }
}
