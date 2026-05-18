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

        public void SaveSettings() => Save(true);
    }
}
