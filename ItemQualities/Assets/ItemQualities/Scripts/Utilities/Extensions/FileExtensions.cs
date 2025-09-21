using System.Globalization;
using System.IO;

namespace ItemQualities.Utilities.Extensions
{
    public static class FileExtensions
    {
        public static bool IsChildOf(this DirectoryInfo dir, DirectoryInfo other)
        {
            return dir != null && other != null && dir.FullName.StartsWith(other.FullName, true, CultureInfo.InvariantCulture);
        }
    }
}
