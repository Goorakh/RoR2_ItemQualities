using RoR2;
using System.IO;

using Path = System.IO.Path;

namespace ItemQualities
{
    static class LanguageFolderHandler
    {
        public static void Register(string searchFolder, string langFolderName = "lang")
        {
            string langFolderPath = Path.Combine(searchFolder, langFolderName);
            if (Directory.Exists(langFolderPath))
            {
                Log.Debug($"Found lang folder: {langFolderPath}");

                Language.collectLanguageRootFolders += folders =>
                {
                    folders.Add(langFolderPath);
                };
            }
            else
            {
                Log.Error($"Lang folder not found: {langFolderPath}");
            }
        }
    }
}
