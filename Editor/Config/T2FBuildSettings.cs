using UnityEditor;
using UnityEngine;

namespace T2FBuild.Editor
{
    [FilePath("ProjectSettings/T2FBuildSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    public class T2FBuildSettings : ScriptableSingleton<T2FBuildSettings>
    {
        [Header("Project")]
        public string projectId = "";

        [Header("Asset Bundle")]
        public string assetBundleProvider = "Addressables";

        public string abRemotePrefixTemplate = "{project}/ab/{target}{profileSuffix}/{env}/{version}/";

        [Header("Upload")]
        public string defaultUploader = "TencentCos";

        public string playerRemotePrefixTemplate = "{project}/webgl/{env}/{version}/";

        public bool uploadEnabledByDefault;

        [Header("Tencent COS")]
        public string tencentCosBucket = "";

        public string tencentCosRegion = "";

        [Header("WeChat MiniGame")]
        public string wechatAppId = "";

        public string wechatCdnBaseUrl = "";

        public string wechatCustomNodePath = "";

        public string wechatFirstPackageGlob = "webgl.data*";

        public string wechatFirstPackageRemotePrefixTemplate = "{project}/wechat/{env}/{version}/data/";

        public void SaveSettings() => Save(true);
    }
}
