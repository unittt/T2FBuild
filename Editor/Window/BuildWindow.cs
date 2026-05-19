using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace T2FBuild.Editor
{
    public class BuildWindow : EditorWindow
    {
        const string MenuPath = "Window/T2FBuild/Build";

        const string EnvVarUploadEnabled = "T2FBUILD_UPLOAD_ENABLED";

        const string PrefTargetKey = "T2FBuild.BuildWindow.Target";

        const string PrefVersionKey = "T2FBuild.BuildWindow.Version";

        const string PrefEnvKey = "T2FBuild.BuildWindow.Env";

        const string PrefBuildNumberKey = "T2FBuild.BuildWindow.BuildNumber";

        const string PrefUploadKey = "T2FBuild.BuildWindow.UploadEnabled";

        static readonly string[] EnvOptions = { "dev", "staging", "prod" };

        struct BuilderOption
        {
            public BuildTarget Target;

            public string Profile;

            public string DisplayName;

            public string PrefId;
        }

        BuilderOption[] _options = Array.Empty<BuilderOption>();

        int _selectedIndex;

        string _version = "0.0.1";

        int _envIndex;

        int _buildNumber;

        bool _uploadEnabled;

        string _lastStatus = string.Empty;

        MessageType _lastStatusType = MessageType.None;

        Vector2 _scroll;

        [MenuItem(MenuPath)]
        public static void Open()
        {
            var win = GetWindow<BuildWindow>("T2FBuild Build");
            win.minSize = new Vector2(480, 380);
        }

        void OnEnable()
        {
            RefreshBuilders();
            LoadPrefs();
        }

        void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.LabelField("T2FBuild — Local Build", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            if (_options.Length == 0)
            {
                EditorGUILayout.HelpBox(
                    "No IPlatformBuilder registered. Make sure at least one platform's asmdef compiles " +
                    "(WebGL is included by default; WeChat needs com.qq.weixin.minigame; etc.).",
                    MessageType.Warning);
                if (GUILayout.Button("Refresh")) RefreshBuilders();
                EditorGUILayout.EndScrollView();
                return;
            }

            DrawTargetRow();
            EditorGUILayout.Space();
            DrawParameters();
            EditorGUILayout.Space();
            DrawUploadToggle();
            EditorGUILayout.Space();
            DrawBuildButton();
            EditorGUILayout.Space();
            DrawStatus();
            EditorGUILayout.Space();
            DrawFooterTips();

            EditorGUILayout.EndScrollView();
        }

        void DrawTargetRow()
        {
            EditorGUILayout.LabelField("Target", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                var labels = _options.Select(o => new GUIContent(o.DisplayName)).ToArray();
                EditorGUI.BeginChangeCheck();
                var newIndex = EditorGUILayout.Popup(_selectedIndex, labels);
                if (EditorGUI.EndChangeCheck())
                {
                    _selectedIndex = Mathf.Clamp(newIndex, 0, _options.Length - 1);
                    SavePrefs();
                }
                if (GUILayout.Button("Refresh", GUILayout.Width(70))) RefreshBuilders();
            }

            var current = _options[_selectedIndex];
            var profileText = string.IsNullOrEmpty(current.Profile) ? "(none)" : current.Profile;
            EditorGUILayout.LabelField(
                $"BuildTarget={current.Target}, Profile={profileText}, OutputRoot=Build/{current.Target}{(string.IsNullOrEmpty(current.Profile) ? "" : "_" + current.Profile)}/",
                EditorStyles.miniLabel);
        }

        void DrawParameters()
        {
            EditorGUILayout.LabelField("Parameters", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            _version = EditorGUILayout.TextField(
                new GUIContent("Version", "Sets PlayerSettings.bundleVersion and feeds {version} in remote prefix templates."),
                _version);
            _envIndex = EditorGUILayout.Popup(
                new GUIContent("Env", "Feeds {env} token in remote prefix templates."),
                _envIndex, EnvOptions);
            _buildNumber = EditorGUILayout.IntField(
                new GUIContent("Build Number", "Android bundleVersionCode / iOS buildNumber. WebGL ignores."),
                _buildNumber);
            if (EditorGUI.EndChangeCheck()) SavePrefs();
        }

        void DrawUploadToggle()
        {
            EditorGUILayout.LabelField("Upload", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            _uploadEnabled = EditorGUILayout.Toggle(
                new GUIContent("Upload to COS",
                    "Per-run override for T2FBUILD_UPLOAD_ENABLED. " +
                    "When checked, runs UploadAssetBundleStep (and WeChat first-package upload) after build. " +
                    "When unchecked, only generates manifest files locally."),
                _uploadEnabled);
            if (EditorGUI.EndChangeCheck()) SavePrefs();

            if (_uploadEnabled)
            {
                EditorGUILayout.HelpBox(
                    "Upload is ENABLED — credentials will be loaded from envs.yml in the project root " +
                    "(configure via Edit > Project Settings > T2FBuild > Secrets). " +
                    "If envs.yml is missing, falls back to TENCENT_SECRET_ID / TENCENT_SECRET_KEY / COS_BUCKET / COS_REGION in shell env.",
                    MessageType.Info);
            }
        }

        void DrawBuildButton()
        {
            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(_version)))
            {
                if (GUILayout.Button("Build", GUILayout.Height(38)))
                {
                    RunBuild();
                }
            }
        }

        void DrawStatus()
        {
            if (!string.IsNullOrEmpty(_lastStatus))
            {
                EditorGUILayout.HelpBox(_lastStatus, _lastStatusType);
            }
        }

        void DrawFooterTips()
        {
            EditorGUILayout.LabelField("Tips", EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField("• Watch the Console for per-step progress and errors.", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("• Output goes to Build/<Target>[_<profile>]/ (relative to project root).", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("• To upload, fill Secrets section in Edit > Project Settings > T2FBuild (writes to envs.yml).", EditorStyles.miniLabel);
        }

        void RefreshBuilders()
        {
            var rawOptions = new List<BuilderOption>();
            try
            {
                foreach (var (target, profile) in PlatformBuilderRegistry.GetAll())
                {
                    rawOptions.Add(new BuilderOption
                    {
                        Target = target,
                        Profile = profile,
                        DisplayName = string.IsNullOrEmpty(profile) ? target.ToString() : $"{target} ({profile})",
                        PrefId = string.IsNullOrEmpty(profile) ? target.ToString() : $"{target}+{profile}",
                    });
                }
            }
            catch (Exception e)
            {
                _lastStatus = $"Failed to enumerate builders: {e.Message}";
                _lastStatusType = MessageType.Error;
            }
            _options = rawOptions.ToArray();
            _selectedIndex = Mathf.Clamp(_selectedIndex, 0, Math.Max(0, _options.Length - 1));
        }

        void LoadPrefs()
        {
            var savedId = EditorPrefs.GetString(PrefTargetKey, string.Empty);
            if (!string.IsNullOrEmpty(savedId))
            {
                for (var i = 0; i < _options.Length; i++)
                {
                    if (_options[i].PrefId == savedId) { _selectedIndex = i; break; }
                }
            }

            _version = EditorPrefs.GetString(PrefVersionKey, "0.0.1");
            var savedEnv = EditorPrefs.GetString(PrefEnvKey, "dev");
            _envIndex = Array.IndexOf(EnvOptions, savedEnv);
            if (_envIndex < 0) _envIndex = 0;
            _buildNumber = EditorPrefs.GetInt(PrefBuildNumberKey, 0);

            var settings = T2FBuildSettings.instance;
            var defaultUpload = settings != null && settings.uploadEnabledByDefault;
            _uploadEnabled = EditorPrefs.GetBool(PrefUploadKey, defaultUpload);
        }

        void SavePrefs()
        {
            if (_options.Length > 0)
            {
                EditorPrefs.SetString(PrefTargetKey, _options[_selectedIndex].PrefId);
            }
            EditorPrefs.SetString(PrefVersionKey, _version ?? string.Empty);
            EditorPrefs.SetString(PrefEnvKey, EnvOptions[Mathf.Clamp(_envIndex, 0, EnvOptions.Length - 1)]);
            EditorPrefs.SetInt(PrefBuildNumberKey, _buildNumber);
            EditorPrefs.SetBool(PrefUploadKey, _uploadEnabled);
        }

        void RunBuild()
        {
            if (_options.Length == 0) return;

            var option = _options[_selectedIndex];
            var env = EnvOptions[Mathf.Clamp(_envIndex, 0, EnvOptions.Length - 1)];
            var ctx = BuildContext.FromValues(option.Target, option.Profile, _version, _buildNumber, env);

            var startedAt = DateTime.Now;
            var label = string.IsNullOrEmpty(option.Profile)
                ? option.Target.ToString()
                : $"{option.Target} ({option.Profile})";

            EditorUtility.DisplayProgressBar("T2FBuild", $"Building {label}…", 0.5f);

            var envsPath = EnvsYmlFile.ResolveDefaultPath();
            var injectedOriginals = EnvsYmlFile.InjectIntoProcess(envsPath);
            var envsLoadedCount = injectedOriginals.Count;

            if (!injectedOriginals.ContainsKey(EnvVarUploadEnabled))
            {
                injectedOriginals[EnvVarUploadEnabled] = Environment.GetEnvironmentVariable(EnvVarUploadEnabled);
            }

            try
            {
                Environment.SetEnvironmentVariable(EnvVarUploadEnabled, _uploadEnabled ? "true" : "false");
                BuildRunner.Execute(ctx);

                var elapsed = DateTime.Now - startedAt;
                var outputAbs = Path.GetFullPath(ctx.OutputRoot);
                var envsSummary = envsLoadedCount > 0
                    ? $"  Loaded {envsLoadedCount} env vars from envs.yml.\n"
                    : "  No envs.yml found — used shell env vars only.\n";
                _lastStatus =
                    $"Build succeeded: {label}\n" +
                    $"  Version: {ctx.Version}, Env: {ctx.Env}, Upload: {_uploadEnabled}\n" +
                    envsSummary +
                    $"  Output: {outputAbs}\n" +
                    $"  Elapsed: {elapsed.TotalSeconds:F1}s";
                _lastStatusType = MessageType.Info;
            }
            catch (Exception e)
            {
                _lastStatus = $"Build FAILED: {label}\n  {e.Message}\n\nSee Console for full trace.";
                _lastStatusType = MessageType.Error;
                Debug.LogException(e);
            }
            finally
            {
                EnvsYmlFile.RestoreProcess(injectedOriginals);
                EditorUtility.ClearProgressBar();
            }
        }
    }
}
