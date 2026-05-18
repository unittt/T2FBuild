using System.Collections.Generic;
using UnityEditor;

namespace T2FBuild.Editor
{
    [PlatformBuilder(BuildTarget.WebGL)]
    public class WebGLBuilder : IPlatformBuilder
    {
        const string DefaultAssetBundleProvider = "Addressables";

        public IEnumerable<IBuildStep> GetSteps(BuildContext ctx) => new IBuildStep[]
        {
            new SwitchPlatformStep(),
            new ApplyVersionStep(),
            new BuildAssetBundleStep(DefaultAssetBundleProvider),
            new BuildPlayerStep(),
        };
    }
}
