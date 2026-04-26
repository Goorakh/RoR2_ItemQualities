using HG;

namespace ItemQualities.ContentManagement
{
    internal sealed class ContentIntializerArgs
    {
        public ExtendedContentPack ContentPack { get; }

        public ReadableProgress<float> ProgressReceiver { get; }

        public ContentIntializerArgs(ExtendedContentPack contentPack, ReadableProgress<float> progressReceiver)
        {
            ContentPack = contentPack;
            ProgressReceiver = progressReceiver;
        }
    }
}
