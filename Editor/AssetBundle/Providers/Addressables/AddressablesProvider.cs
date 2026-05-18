using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;

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

            return new AssetBundleBuildResult
            {
                Success = string.IsNullOrEmpty(result.Error),
                Error = result.Error,
                OutputDirectory = result.OutputPath,
                Version = version,
            };
        }
    }
}
