using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace T2FBuild.Editor
{
    static class T2FBuildSettingsProvider
    {
        const string ProviderPath = "Project/T2FBuild";

        const string NotInstalledSuffix = "  (not installed)";

        [SettingsProvider]
        public static SettingsProvider Create()
        {
            return new SettingsProvider(ProviderPath, SettingsScope.Project)
            {
                label = "T2FBuild",
                guiHandler = OnGUI,
                keywords = new HashSet<string>(new[]
                {
                    "T2F", "T2FBuild", "Build", "Asset", "Bundle", "Upload", "COS", "Addressables", "Provider", "Uploader"
                }),
            };
        }

        static void OnGUI(string searchContext)
        {
            var settings = T2FBuildSettings.instance;
            EditorGUI.BeginChangeCheck();

            EditorGUILayout.LabelField("Asset Bundle", EditorStyles.boldLabel);
            settings.assetBundleProvider = DrawNamedRegistryDropdown(
                new GUIContent("Provider",
                    "Name of the IAssetBundleProvider to use. Populated from classes decorated with [AssetBundleProvider(\"...\")]."),
                settings.assetBundleProvider,
                AssetBundleProviderRegistry.GetInstalledNames(),
                "No IAssetBundleProvider found. Install a provider package (e.g. com.unity.addressables) for it to appear.");
            settings.abRemotePrefixTemplate = EditorGUILayout.TextField(
                new GUIContent("Remote Prefix Template",
                    "Used by GenerateUploadManifestStep. Tokens: {target} {profile} {env} {version}."),
                settings.abRemotePrefixTemplate);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Upload", EditorStyles.boldLabel);
            settings.defaultUploader = DrawNamedRegistryDropdown(
                new GUIContent("Default Uploader",
                    "Name of the IAssetBundleUploader to use. Populated from classes decorated with [AssetBundleUploader(\"...\")]."),
                settings.defaultUploader,
                AssetBundleUploaderRegistry.GetInstalledNames(),
                "No IAssetBundleUploader found.");
            settings.playerRemotePrefixTemplate = EditorGUILayout.TextField(
                new GUIContent("Player Remote Prefix",
                    "Used when uploading the WebGL Player as a static site to COS. Tokens: {env} {version}. Referenced from the CI workflow yml."),
                settings.playerRemotePrefixTemplate);
            settings.uploadEnabledByDefault = EditorGUILayout.Toggle(
                new GUIContent("Enabled By Default",
                    "Fallback for UploadAssetBundleStep when the T2FBUILD_UPLOAD_ENABLED env var is unset. CI workflows should set the env var explicitly; this toggle is for local dev convenience."),
                settings.uploadEnabledByDefault);

            if (EditorGUI.EndChangeCheck())
            {
                settings.SaveSettings();
            }
        }

        static string DrawNamedRegistryDropdown(GUIContent label, string current, IEnumerable<string> installed, string emptyMessage)
        {
            var installedList = installed?.OrderBy(n => n).ToList() ?? new List<string>();

            if (installedList.Count == 0)
            {
                EditorGUILayout.HelpBox(emptyMessage, MessageType.Warning);
                EditorGUILayout.LabelField(label, new GUIContent(string.IsNullOrEmpty(current) ? "<unset>" : current));
                return current;
            }

            var values = new List<string>(installedList);
            var display = installedList.ToList();
            var currentIndex = values.IndexOf(current);

            if (currentIndex < 0 && !string.IsNullOrEmpty(current))
            {
                values.Insert(0, current);
                display.Insert(0, current + NotInstalledSuffix);
                currentIndex = 0;
            }
            else if (currentIndex < 0)
            {
                currentIndex = 0;
            }

            var newIndex = EditorGUILayout.Popup(label, currentIndex, display.ToArray());
            return values[newIndex];
        }
    }
}
