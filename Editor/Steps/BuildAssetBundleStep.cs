using System;
using UnityEngine;

namespace T2FBuild.Editor
{
    public class BuildAssetBundleStep : IBuildStep
    {
        readonly string _providerName;

        public BuildAssetBundleStep(string providerName)
        {
            _providerName = providerName;
        }

        public string Name => $"Build Asset Bundle ({_providerName})";

        public void Execute(BuildContext ctx)
        {
            var provider = AssetBundleProviderRegistry.Get(_providerName);
            var result = provider.Build(ctx);

            if (!result.Success)
            {
                throw new Exception($"[T2FBuild] Asset bundle build failed: {result.Error}");
            }

            ctx.Extras["ab.provider"] = _providerName;
            ctx.Extras["ab.outputDir"] = result.OutputDirectory;
            ctx.Extras["ab.version"] = result.Version;
            ctx.Extras["ab.manifestPath"] = result.ManifestPath;

            Debug.Log($"[T2FBuild] Asset bundle built: provider={_providerName}, version={result.Version}, output={result.OutputDirectory}");
        }
    }
}
