using UnityEditor;
using UnityEngine;

namespace T2FBuild.Editor
{
    [FilePath("ProjectSettings/T2FBuildSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    public class T2FBuildSettings : ScriptableSingleton<T2FBuildSettings>
    {
        [Header("Asset Bundle")]
        public string assetBundleProvider = "Addressables";

        public string abRemotePrefixTemplate = "ab/{target}/{env}/{version}/";

        [Header("Upload")]
        public string defaultUploader = "TencentCos";

        public string playerRemotePrefixTemplate = "webgl/{env}/{version}/";

        public bool uploadEnabledByDefault;

        [Header("WeChat MiniGame")]
        public string wechatAppId = "";

        public string wechatCdnBaseUrl = "";

        public string wechatCustomNodePath = "";

        public string wechatFirstPackageGlob = "webgl.data*";

        public string wechatFirstPackageRemotePrefixTemplate = "wechat/{env}/{version}/data/";

        public int wechatMainPackageSizeLimitMB = 4;

        public void SaveSettings() => Save(true);
    }
}
