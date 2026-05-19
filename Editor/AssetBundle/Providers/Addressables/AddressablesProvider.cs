using System.IO;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace T2FBuild.Editor.Providers
{
    [AssetBundleProvider("Addressables")]
    public class AddressablesProvider : IAssetBundleProvider
    {
        public string Name => "Addressables";

        public AssetBundleBuildResult Build(BuildContext ctx)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                return new AssetBundleBuildResult
                {
                    Success = false,
                    Error = "AddressableAssetSettings not found. Open Window > Asset Management > Addressables > Groups to initialize.",
                };
            }

            AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult result);

            var version = !string.IsNullOrEmpty(settings.OverridePlayerVersion)
                ? settings.OverridePlayerVersion
                : ctx.Version;

            var outputDir = ResolveOutputDirectory(result.OutputPath);
            if (!string.IsNullOrEmpty(outputDir))
            {
                Debug.Log($"[T2FBuild][Addressables] Resolved AB output directory: {outputDir} (raw OutputPath={result.OutputPath})");
            }

            return new AssetBundleBuildResult
            {
                Success = string.IsNullOrEmpty(result.Error),
                Error = result.Error,
                OutputDirectory = outputDir,
                Version = version,
            };
        }

        static string ResolveOutputDirectory(string outputPath)
        {
            if (string.IsNullOrEmpty(outputPath)) return outputPath;
            if (Directory.Exists(outputPath)) return outputPath;
            if (File.Exists(outputPath))
            {
                // Addressables 1.20+ returns the path to settings.json instead of the directory.
                return Path.GetDirectoryName(outputPath);
            }
            return outputPath;
        }
    }
}
