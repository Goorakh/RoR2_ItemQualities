using System;

namespace ItemQualities.ContentManagement
{
    internal sealed class ContentIntializerArgs
    {
        public ExtendedContentPack ContentPack { get; }

        public IProgress<float> ProgressReceiver { get; }

        public ContentIntializerArgs(ExtendedContentPack contentPack, IProgress<float> progressReceiver)
        {
            ContentPack = contentPack;
            ProgressReceiver = progressReceiver;
        }
    }
}
