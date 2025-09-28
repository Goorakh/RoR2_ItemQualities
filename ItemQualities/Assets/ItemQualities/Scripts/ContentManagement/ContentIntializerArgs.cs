using System;

namespace ItemQualities.ContentManagement
{
    public class ContentIntializerArgs
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
