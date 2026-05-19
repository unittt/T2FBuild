using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace T2FBuild.Editor
{
    static class T2FBuildSettingsProvider
    {
        const string ProviderPath = "Project/T2FBuild";

        const string NotInstalledSuffix = "  (not installed)";

        const string KeyTencentSecretId = "TENCENT_SECRET_ID";

        const string KeyTencentSecretKey = "TENCENT_SECRET_KEY";

        const string KeyCosBucket = "COS_BUCKET";

        const string KeyCosRegion = "COS_REGION";

        const string KeyUnityLicense = "UNITY_LICENSE_BASE64";

        static SecretsState _secretsState;

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
                    "WeChat", "MiniGame", "AppId", "Secret", "License", "envs"
                }),
                activateHandler = (_, __) => ReloadSecretsState(),
            };
        }

        static void OnGUI(string searchContext)
        {
            var settings = T2FBuildSettings.instance;
            EditorGUI.BeginChangeCheck();

            EditorGUILayout.LabelField("Project", EditorStyles.boldLabel);
            settings.projectId = EditorGUILayout.TextField(
                new GUIContent("Project ID",
                    "Optional namespace prefix for COS remote paths when a single bucket hosts multiple projects. " +
                    "Replaces {project} token in all remote prefix templates below. " +
                    "Leave empty for single-project buckets. Example: 'bounceblast' → 'bounceblast/ab/WebGL/dev/0.0.1/'."),
                settings.projectId);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Asset Bundle", EditorStyles.boldLabel);
            settings.assetBundleProvider = DrawNamedRegistryDropdown(
                new GUIContent("Provider",
                    "Name of the IAssetBundleProvider to use. Populated from classes decorated with [AssetBundleProvider(\"...\")]."),
                settings.assetBundleProvider,
                AssetBundleProviderRegistry.GetInstalledNames(),
                "No IAssetBundleProvider found. Install a provider package (e.g. com.unity.addressables) for it to appear.");
            settings.abRemotePrefixTemplate = EditorGUILayout.TextField(
                new GUIContent("Remote Prefix Template",
                    "Used by GenerateUploadManifestStep. Tokens: {project} {target} {profile} {profileSuffix} {env} {version}. " +
                    "{project}/ resolves to empty when Project ID is empty. {profileSuffix} resolves to '_<profile>' or empty " +
                    "— use it to namespace multi-profile builds (e.g. WebGL vs WebGL+wechat) without forcing an extra path segment when profile is empty."),
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
                    "Used when uploading the WebGL Player as a static site to COS. Tokens: {project} {env} {version}. " +
                    "Referenced from the CI workflow yml via $BUILD_PROJECT etc.; keep the template here aligned with the yml."),
                settings.playerRemotePrefixTemplate);
            settings.uploadEnabledByDefault = EditorGUILayout.Toggle(
                new GUIContent("Enabled By Default",
                    "Fallback for UploadAssetBundleStep when the T2FBUILD_UPLOAD_ENABLED env var is unset. CI workflows should set the env var explicitly; this toggle is for local dev convenience."),
                settings.uploadEnabledByDefault);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Tencent COS", EditorStyles.boldLabel);
            settings.tencentCosBucket = EditorGUILayout.TextField(
                new GUIContent("Bucket",
                    "COS bucket name (public identifier, not a secret). Used as the upload target Bucket and as fallback to derive the WeChat CDN URL when WeChat MiniGame > CDN Base URL is empty."),
                settings.tencentCosBucket);
            settings.tencentCosRegion = EditorGUILayout.TextField(
                new GUIContent("Region",
                    "COS bucket region (e.g. ap-shanghai). Public identifier, not a secret. Combined with Bucket above when deriving the COS endpoint URL."),
                settings.tencentCosRegion);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("WeChat MiniGame", EditorStyles.boldLabel);
            settings.wechatAppId = EditorGUILayout.TextField(
                new GUIContent("AppId",
                    "WeChat MiniGame AppId (wxXXXXXXXXXXXXXXXX). Written into MiniGameConfig.asset before each WeChat export."),
                settings.wechatAppId);
            settings.wechatCdnBaseUrl = EditorGUILayout.TextField(
                new GUIContent("CDN Base URL",
                    "Optional override for the runtime CDN URL written into MiniGameConfig.CDN (e.g. https://cdn.example.com/). Leave empty to auto-derive from Tencent COS Bucket+Region above (direct COS hosting). Fill only when serving WeChat first-package data through a CDN that fronts COS."),
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
                    "COS key prefix for the first-package data files. Tokens: {project} {env} {version} {profile} {target}. The same prefix is written into MiniGameConfig.CDN so the runtime fetches them from CDN."),
                settings.wechatFirstPackageRemotePrefixTemplate);

            if (EditorGUI.EndChangeCheck())
            {
                settings.SaveSettings();
            }

            EditorGUILayout.Space();
            DrawSecretsSection();
        }

        static void DrawSecretsSection()
        {
            if (_secretsState == null) ReloadSecretsState();

            EditorGUILayout.LabelField("Secrets (envs.yml)", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                $"Stored in {_secretsState.Path ?? "<unresolved>"} — NOT in this .asset.",
                EditorStyles.miniLabel);
            EditorGUILayout.LabelField(
                _secretsState.FileExists ? "File exists — fields below show current values." : "File does not exist — saving will create it.",
                EditorStyles.miniLabel);

            EditorGUI.BeginChangeCheck();
            _secretsState.SecretId = EditorGUILayout.TextField(
                new GUIContent("Tencent Secret ID", "From https://console.cloud.tencent.com/cam/capi. Stored in envs.yml as TENCENT_SECRET_ID."),
                _secretsState.SecretId ?? string.Empty);
            _secretsState.SecretKey = EditorGUILayout.PasswordField(
                new GUIContent("Tencent Secret Key", "From CAM. Masked display; underlying value is copyable. Stored in envs.yml as TENCENT_SECRET_KEY."),
                _secretsState.SecretKey ?? string.Empty);
            if (EditorGUI.EndChangeCheck()) _secretsState.Dirty = true;

            DrawLicenseRow();

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!_secretsState.Dirty))
                {
                    if (GUILayout.Button("Save to envs.yml", GUILayout.Height(22)))
                    {
                        SaveSecrets();
                    }
                }
                if (GUILayout.Button("Reload", GUILayout.Height(22), GUILayout.Width(80)))
                {
                    ReloadSecretsState();
                }
            }

            if (!string.IsNullOrEmpty(_secretsState.Status))
            {
                EditorGUILayout.HelpBox(_secretsState.Status, _secretsState.StatusType);
            }
        }

        static void DrawLicenseRow()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                var label = string.IsNullOrEmpty(_secretsState.LicenseBase64)
                    ? "<not set>"
                    : $"{_secretsState.LicenseBase64.Length} chars";
                EditorGUILayout.LabelField(
                    new GUIContent("Unity License (base64)", "base64-encoded contents of Unity_lic.ulf, written to envs.yml as UNITY_LICENSE_BASE64. CI uses it to activate Unity in batch mode."),
                    new GUIContent(label));
                if (GUILayout.Button("Load from .ulf...", GUILayout.Width(140)))
                {
                    LoadLicenseFromUlf();
                }
            }
        }

        static void ReloadSecretsState()
        {
            _secretsState = new SecretsState
            {
                Path = EnvsYmlFile.ResolveDefaultPath(),
            };
            _secretsState.FileExists = !string.IsNullOrEmpty(_secretsState.Path) && File.Exists(_secretsState.Path);

            if (_secretsState.FileExists)
            {
                EnvsYmlFile.TryRead(_secretsState.Path, KeyTencentSecretId, out var sid);
                EnvsYmlFile.TryRead(_secretsState.Path, KeyTencentSecretKey, out var sk);
                EnvsYmlFile.TryRead(_secretsState.Path, KeyUnityLicense, out var lic);
                _secretsState.SecretId = EnvsYmlFile.IsPlaceholder(sid) ? string.Empty : sid;
                _secretsState.SecretKey = EnvsYmlFile.IsPlaceholder(sk) ? string.Empty : sk;
                _secretsState.LicenseBase64 = EnvsYmlFile.IsPlaceholder(lic)
                    ? string.Empty
                    : (lic ?? string.Empty).Replace("\n", string.Empty).Trim();
            }
            _secretsState.Dirty = false;
            _secretsState.Status = string.Empty;
            _secretsState.StatusType = MessageType.None;
        }

        static void SaveSecrets()
        {
            if (_secretsState == null || string.IsNullOrEmpty(_secretsState.Path))
            {
                _secretsState.Status = "envs.yml path could not be resolved.";
                _secretsState.StatusType = MessageType.Error;
                return;
            }

            var updates = new List<EnvsYmlFile.EnvField>();
            if (!string.IsNullOrEmpty(_secretsState.SecretId)) updates.Add(new EnvsYmlFile.EnvField { Key = KeyTencentSecretId, Value = _secretsState.SecretId });
            if (!string.IsNullOrEmpty(_secretsState.SecretKey)) updates.Add(new EnvsYmlFile.EnvField { Key = KeyTencentSecretKey, Value = _secretsState.SecretKey });
            if (!string.IsNullOrEmpty(_secretsState.LicenseBase64)) updates.Add(new EnvsYmlFile.EnvField { Key = KeyUnityLicense, Value = _secretsState.LicenseBase64, IsBlock = true });

            var settings = T2FBuildSettings.instance;
            if (!string.IsNullOrEmpty(settings.tencentCosBucket)) updates.Add(new EnvsYmlFile.EnvField { Key = KeyCosBucket, Value = settings.tencentCosBucket });
            if (!string.IsNullOrEmpty(settings.tencentCosRegion)) updates.Add(new EnvsYmlFile.EnvField { Key = KeyCosRegion, Value = settings.tencentCosRegion });

            if (updates.Count == 0)
            {
                _secretsState.Status = "Nothing to write — all secret fields are empty.";
                _secretsState.StatusType = MessageType.Warning;
                return;
            }

            try
            {
                EnvsYmlFile.Write(_secretsState.Path, updates);
                AssetDatabase.Refresh();
                _secretsState.Dirty = false;
                _secretsState.FileExists = true;
                _secretsState.Status =
                    $"Wrote {updates.Count} field(s) to {Path.GetFileName(_secretsState.Path)}. " +
                    $"Bucket / Region copied from Tencent COS section above.";
                _secretsState.StatusType = MessageType.Info;
            }
            catch (Exception e)
            {
                _secretsState.Status = $"Failed to write envs.yml: {e.Message}";
                _secretsState.StatusType = MessageType.Error;
            }
        }

        static void LoadLicenseFromUlf()
        {
            var defaultPath = DetectDefaultUlfPath();
            var startDir = string.IsNullOrEmpty(defaultPath) ? string.Empty : Path.GetDirectoryName(defaultPath);
            var picked = EditorUtility.OpenFilePanel("Select Unity_lic.ulf", startDir, "ulf");
            if (string.IsNullOrEmpty(picked)) return;
            try
            {
                var bytes = File.ReadAllBytes(picked);
                _secretsState.LicenseBase64 = Convert.ToBase64String(bytes);
                _secretsState.Dirty = true;
                _secretsState.Status = $"Encoded .ulf: {bytes.Length} bytes → base64 {_secretsState.LicenseBase64.Length} chars. Click Save to write to envs.yml.";
                _secretsState.StatusType = MessageType.Info;
            }
            catch (Exception e)
            {
                _secretsState.Status = $"Failed to read .ulf: {e.Message}";
                _secretsState.StatusType = MessageType.Error;
            }
        }

        static string DetectDefaultUlfPath()
        {
            var candidates = new List<string>
            {
                @"C:\ProgramData\Unity\Unity_lic.ulf",
                "/Library/Application Support/Unity/Unity_lic.ulf",
                "/var/lib/unity/Unity_lic.ulf",
            };
            var home = Environment.GetEnvironmentVariable("HOME");
            if (!string.IsNullOrEmpty(home))
            {
                candidates.Add(Path.Combine(home, ".local/share/unity3d/Unity/Unity_lic.ulf"));
            }
            foreach (var path in candidates)
            {
                if (File.Exists(path)) return path;
            }
            return Application.platform switch
            {
                RuntimePlatform.WindowsEditor => candidates[0],
                RuntimePlatform.OSXEditor => candidates[1],
                _ => candidates[2],
            };
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

        class SecretsState
        {
            public string Path;

            public bool FileExists;

            public string SecretId;

            public string SecretKey;

            public string LicenseBase64;

            public bool Dirty;

            public string Status;

            public MessageType StatusType;
        }
    }
}
