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
                    "T2F", "T2FBuild", "Build", "Asset", "Bundle", "Upload", "COS", "Addressables", "Provider", "Uploader",
                    "WeChat", "MiniGame", "AppId"
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

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("WeChat MiniGame", EditorStyles.boldLabel);
            settings.wechatAppId = EditorGUILayout.TextField(
                new GUIContent("AppId",
                    "WeChat MiniGame AppId (wxXXXXXXXXXXXXXXXX). Written into MiniGameConfig.asset before each WeChat export."),
                settings.wechatAppId);
            settings.wechatCdnBaseUrl = EditorGUILayout.TextField(
                new GUIContent("CDN Base URL",
                    "Full CDN/COS URL where first-package data files will be served from (e.g. https://mybucket-123.cos.ap-shanghai.myqcloud.com/). Combined with First Package Remote Prefix to produce the full CDN URL written into MiniGameConfig.CDN."),
                settings.wechatCdnBaseUrl);
            settings.wechatCustomNodePath = EditorGUILayout.TextField(
                new GUIContent("Custom Node Path",
                    "Absolute path to node.exe. Leave empty to use the node on system PATH (CI installs node via actions/setup-node)."),
                settings.wechatCustomNodePath);
            settings.wechatFirstPackageGlob = EditorGUILayout.TextField(
                new GUIContent("First Package Glob",
                    "Glob (relative to <DST>/minigame/) selecting heavy first-package data files that must be served from CDN. Default matches Unity's webgl.data* family."),
                settings.wechatFirstPackageGlob);
            settings.wechatFirstPackageRemotePrefixTemplate = EditorGUILayout.TextField(
                new GUIContent("First Package Remote Prefix",
                    "COS key prefix for the first-package data files. Tokens: {env} {version} {profile} {target}. The same prefix is written into MiniGameConfig.CDN so the runtime fetches them from CDN."),
                settings.wechatFirstPackageRemotePrefixTemplate);
            settings.wechatMainPackageSizeLimitMB = EditorGUILayout.IntField(
                new GUIContent("Main Package Size Limit (MB)",
                    "WeChat MiniGame main package size cap (4 MB platform limit). ValidateWeChatPackageSizeStep fails the build if the post-export main package exceeds this — first-package files matched by the glob above are excluded from the count."),
                settings.wechatMainPackageSizeLimitMB);

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
