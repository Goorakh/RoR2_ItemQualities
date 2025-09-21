using ItemQualities.Utilities.Extensions;
using System;
using System.IO;
using System.Linq;

namespace ItemQualities.Utilities
{
    public static class FileUtils
    {
        public static FileInfo SearchUpwards(DirectoryInfo startDir, DirectoryInfo endDir, string searchPattern)
        {
            if (startDir is null)
                throw new ArgumentNullException(nameof(startDir));

            if (endDir is null)
                throw new ArgumentNullException(nameof(endDir));

            if (string.IsNullOrEmpty(searchPattern))
                throw new ArgumentException($"'{nameof(searchPattern)}' cannot be null or empty.", nameof(searchPattern));

            if (!startDir.IsChildOf(endDir))
                return null;

            DirectoryInfo current = startDir;
            while (current != null)
            {
                FileInfo file = current.EnumerateFiles(searchPattern, SearchOption.TopDirectoryOnly).FirstOrDefault();
                if (file != null)
                    return file;

                if (string.Equals(current.FullName, endDir.FullName, StringComparison.OrdinalIgnoreCase))
                    break;

                current = current.Parent;
            }

            return null;
        }

        public static FileInfo SearchUpwards(DirectoryInfo startDir, string searchPattern)
        {
            return SearchUpwards(startDir, startDir.Root, searchPattern);
        }
    }
}
