using Path = System.IO.Path;

namespace ItemQualities.Caching
{
    internal static class CacheFileHelper
    {
        private static readonly string _cacheDirectoryPath = Path.Combine(ItemQualitiesPlugin.PersistentDataPath, "cache");

        public static string GetCacheFolderPath(string cacheName)
        {
            return Path.Combine(_cacheDirectoryPath, cacheName);
        }
    }
}
