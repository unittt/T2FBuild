using System.Collections.Generic;
using UnityEditor;

namespace T2FBuild.Editor
{
    [PlatformBuilder(BuildTarget.WebGL)]
    public class WebGLBuilder : IPlatformBuilder
    {
        public IEnumerable<IBuildStep> GetSteps(BuildContext ctx)
        {
            var settings = T2FBuildSettings.instance;
            return new IBuildStep[]
            {
                new SwitchPlatformStep(),
                new ApplyVersionStep(),
                new BuildAssetBundleStep(settings.assetBundleProvider),
                new GenerateUploadManifestStep(settings.abRemotePrefixTemplate),
                new UploadAssetBundleStep(settings.defaultUploader),
                new BuildPlayerStep(),
                new UploadPlayerStep(settings.defaultUploader, settings.playerRemotePrefixTemplate),
            };
        }
    }
}
