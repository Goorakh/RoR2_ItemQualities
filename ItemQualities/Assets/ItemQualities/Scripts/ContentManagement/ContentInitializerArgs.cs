using HG;

namespace ItemQualities.ContentManagement
{
    internal sealed class ContentInitializerArgs
    {
        public ExtendedContentPack ContentPack { get; }

        public ReadableProgress<float> ProgressReceiver { get; }

        public ContentInitializerArgs(ExtendedContentPack contentPack, ReadableProgress<float> progressReceiver)
        {
            ContentPack = contentPack;
            ProgressReceiver = progressReceiver;
        }
    }
}
