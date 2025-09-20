using RoR2.ContentManagement;
using System;
using System.Collections;
using System.Reflection;

using Path = System.IO.Path;

namespace ItemQualities
{
    public class ItemQualitiesContent : IContentPackProvider
    {
        readonly ContentPack _contentPack = new ContentPack();

        public string identifier => ItemQualitiesPlugin.PluginGUID;

        internal ItemQualitiesContent()
        {
        }

        internal void Register()
        {
            ContentManager.collectContentPackProviders += collectContentPackProviders;
        }

        void collectContentPackProviders(ContentManager.AddContentPackProviderDelegate addContentPackProvider)
        {
            addContentPackProvider(this);
        }

        public IEnumerator LoadStaticContentAsync(LoadStaticContentAsyncArgs args)
        {
            args.ReportProgress(1f);
            yield break;
        }

        public IEnumerator GenerateContentPackAsync(GetContentPackAsyncArgs args)
        {
            ContentPack.Copy(_contentPack, args.output);
            args.ReportProgress(1f);
            yield break;
        }

        public IEnumerator FinalizeAsync(FinalizeAsyncArgs args)
        {
            args.ReportProgress(1f);
            yield break;
        }

        static void populateTypeFields<TAsset>(Type typeToPopulate, NamedAssetCollection<TAsset> assets, Func<string, string> fieldNameToAssetNameConverter = null)
        {
            foreach (FieldInfo fieldInfo in typeToPopulate.GetFields(BindingFlags.Static | BindingFlags.Public))
            {
                if (!typeof(TAsset).IsAssignableFrom(fieldInfo.FieldType))
                    continue;

                TargetAssetNameAttribute customAttribute = fieldInfo.GetCustomAttribute<TargetAssetNameAttribute>();
                string assetName;
                if (customAttribute != null)
                {
                    assetName = customAttribute.targetAssetName;
                }
                else if (fieldNameToAssetNameConverter != null)
                {
                    assetName = fieldNameToAssetNameConverter(fieldInfo.Name);
                }
                else
                {
                    assetName = fieldInfo.Name;
                }

                TAsset tasset = assets.Find(assetName);
                if (tasset != null)
                {
                    fieldInfo.SetValue(null, tasset);
                }
                else
                {
                    Log.Warning($"Failed to assign {fieldInfo.DeclaringType.Name}.{fieldInfo.Name}: Asset \"{assetName}\" not found");
                }
            }
        }
    }
}
