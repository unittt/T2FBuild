using System.Collections.Generic;
using UnityEditor;

namespace T2FBuild.Editor
{
    [PlatformBuilder(BuildTarget.WebGL)]
    public class WebGLBuilder : IPlatformBuilder
    {
        const string DefaultAssetBundleProvider = "Addressables";

        const string DefaultUploader = "TencentCos";

        public IEnumerable<IBuildStep> GetSteps(BuildContext ctx) => new IBuildStep[]
        {
            new SwitchPlatformStep(),
            new ApplyVersionStep(),
            new BuildAssetBundleStep(DefaultAssetBundleProvider),
            new GenerateUploadManifestStep(),
            new UploadAssetBundleStep(DefaultUploader),
            new BuildPlayerStep(),
        };
    }
}
