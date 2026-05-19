#if T2FBUILD_HAS_WECHAT
using System.Collections.Generic;
using UnityEditor;

namespace T2FBuild.Editor.Platforms.WeChat
{
    [PlatformBuilder(BuildTarget.WebGL, "wechat")]
    public class WeChatBuilder : IPlatformBuilder
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
                new ConfigureWeChatProjectStep(),
                new RunWeChatExportStep(),
                new ValidateWeChatPackageSizeStep(settings.wechatMainPackageSizeLimitMB),
                new UploadWeChatFirstPackageStep(settings.defaultUploader),
            };
        }
    }
}
#endif
