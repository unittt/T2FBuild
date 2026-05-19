using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace T2FBuild.Editor
{
    public class CITemplateInstallerWindow : EditorWindow
    {
        const string MenuPath = "Window/T2FBuild/CI Template Installer";

        const string EditorPrefsPlatformKey = "T2FBuild.CIPlatform";

        static readonly CIPlatformDef[] Platforms =
        {
            new CIPlatformDef
            {
                Id = "github",
                DisplayName = "GitHub Actions",
                TemplatesSubDir = "CI/Templates~/github/workflows",
                ResolveProjectRelTarget = fn => $".github/workflows/{fn}",
                TargetLabel = "Workflows → .github/workflows/",
                OpenButtonLabel = "Open .github/workflows",
                OpenButtonRelPath = ".github/workflows",
                PostInstallNote =
                    "Configure repo Secrets at GitHub repo > Settings > Secrets and variables > Actions:\n" +
                    "  UNITY_LICENSE, TENCENT_SECRET_ID, TENCENT_SECRET_KEY, COS_BUCKET, COS_REGION",
            },
            new CIPlatformDef
            {
                Id = "cnb",
                DisplayName = "CNB",
                TemplatesSubDir = "CI/Templates~/cnb",
                ResolveProjectRelTarget = _ => ".cnb.yml",
                TargetLabel = "Pipeline → <repo>/.cnb.yml",
                OpenButtonLabel = "Open repo root",
                OpenButtonRelPath = ".",
                PostInstallNote =
                    "Edit the imports: URL inside .cnb.yml to point at your secrets file (default: same repo's envs.yml).\n" +
                    "Configure envs.yml via Edit > Project Settings > T2FBuild > Secrets (writes UNITY_LICENSE_BASE64, TENCENT_SECRET_ID/KEY, COS_BUCKET, COS_REGION).",
            },
        };

        readonly List<WorkflowTemplate> _workflows = new List<WorkflowTemplate>();

        bool[] _workflowSelections = Array.Empty<bool>();

        string _packageRoot;

        string _projectRoot;

        int _platformIndex;

        Vector2 _scroll;

        [MenuItem(MenuPath)]
        public static void Open()
        {
            var win = GetWindow<CITemplateInstallerWindow>("T2FBuild CI Installer");
            win.minSize = new Vector2(540, 380);
        }

        void OnEnable()
        {
            _platformIndex = ResolveSavedPlatformIndex();
            Refresh();
        }

        CIPlatformDef CurrentPlatform => Platforms[_platformIndex];

        void Refresh()
        {
            _projectRoot = Path.GetDirectoryName(Application.dataPath);
            _packageRoot = T2FBuildPackagePath.ResolveRoot();
            _workflows.Clear();

            if (string.IsNullOrEmpty(_packageRoot)) return;

            var platform = CurrentPlatform;
            var workflowsDir = Path.Combine(_packageRoot, platform.TemplatesSubDir);
            if (!Directory.Exists(workflowsDir)) return;

            foreach (var path in Directory.GetFiles(workflowsDir, "*.yml").OrderBy(p => p))
            {
                var fileName = Path.GetFileName(path);
                var rel = platform.ResolveProjectRelTarget(fileName);
                _workflows.Add(new WorkflowTemplate
                {
                    FileName = fileName,
                    SourcePath = path,
                    TargetPath = Path.Combine(_projectRoot, rel),
                    TargetRelLabel = rel,
                });
            }

            _workflowSelections = new bool[_workflows.Count];
            for (var i = 0; i < _workflowSelections.Length; i++) _workflowSelections[i] = true;
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("T2FBuild CI Template Installer", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            if (string.IsNullOrEmpty(_packageRoot))
            {
                EditorGUILayout.HelpBox(
                    "Could not resolve the T2FBuild package on disk. " +
                    "Expected either a UPM package (Packages/manifest.json) or an embedded folder (Assets/T2FBuild) with package.json next to the asmdef.",
                    MessageType.Error);
                if (GUILayout.Button("Refresh")) Refresh();
                return;
            }

            DrawPlatformPicker();
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Package", PathOrDash(_packageRoot), EditorStyles.miniLabel);
            EditorGUILayout.LabelField("Project", PathOrDash(_projectRoot), EditorStyles.miniLabel);
            EditorGUILayout.Space();

            if (_workflows.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    $"No templates found under '{CurrentPlatform.TemplatesSubDir}'.",
                    MessageType.Warning);
                if (GUILayout.Button("Refresh")) Refresh();
                return;
            }

            DrawWorkflowList();
            EditorGUILayout.Space();
            DrawApplyButton();
            EditorGUILayout.Space();
            DrawFooterButtons();
        }

        void DrawPlatformPicker()
        {
            var labels = Platforms.Select(p => new GUIContent(p.DisplayName)).ToArray();
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(
                    new GUIContent("CI Platform", "Select the CI/CD platform this project is hosted on. Each platform ships its own set of pipeline templates."),
                    GUILayout.Width(80));
                var newIndex = EditorGUILayout.Popup(_platformIndex, labels);
                if (newIndex != _platformIndex)
                {
                    _platformIndex = newIndex;
                    EditorPrefs.SetString(EditorPrefsPlatformKey, CurrentPlatform.Id);
                    Refresh();
                }
            }
        }

        void DrawWorkflowList()
        {
            EditorGUILayout.LabelField(CurrentPlatform.TargetLabel, EditorStyles.boldLabel);

            _scroll = EditorGUILayout.BeginScrollView(_scroll, EditorStyles.helpBox);
            for (var i = 0; i < _workflows.Count; i++)
            {
                var wf = _workflows[i];
                using (new EditorGUILayout.HorizontalScope())
                {
                    _workflowSelections[i] = EditorGUILayout.ToggleLeft(
                        new GUIContent(wf.FileName, $"Source: {wf.SourcePath}\nTarget: {wf.TargetPath}"),
                        _workflowSelections[i],
                        GUILayout.Width(220));

                    var status = File.Exists(wf.TargetPath) ? "exists (will overwrite)" : "new";
                    EditorGUILayout.LabelField($"→ {wf.TargetRelLabel}  [{status}]", EditorStyles.miniLabel);
                }
            }
            EditorGUILayout.EndScrollView();
        }

        void DrawApplyButton()
        {
            using (new EditorGUI.DisabledScope(!HasAnySelection()))
            {
                if (GUILayout.Button("Apply", GUILayout.Height(30)))
                {
                    Apply();
                }
            }
        }

        void DrawFooterButtons()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Refresh"))
                {
                    Refresh();
                }
                var openPath = Path.Combine(_projectRoot, CurrentPlatform.OpenButtonRelPath);
                using (new EditorGUI.DisabledScope(!Directory.Exists(openPath)))
                {
                    if (GUILayout.Button(CurrentPlatform.OpenButtonLabel))
                    {
                        EditorUtility.RevealInFinder(openPath);
                    }
                }
            }
        }

        bool HasAnySelection()
        {
            for (var i = 0; i < _workflowSelections.Length; i++)
            {
                if (_workflowSelections[i]) return true;
            }
            return false;
        }

        void Apply()
        {
            var summary = new StringBuilder();
            var copied = 0;
            var failed = 0;

            for (var i = 0; i < _workflows.Count; i++)
            {
                if (!_workflowSelections[i]) continue;
                var wf = _workflows[i];
                try
                {
                    EnsureDir(Path.GetDirectoryName(wf.TargetPath));
                    File.Copy(wf.SourcePath, wf.TargetPath, overwrite: true);
                    copied++;
                    summary.AppendLine($"✓ {wf.TargetRelLabel}");
                }
                catch (Exception e)
                {
                    failed++;
                    summary.AppendLine($"✗ {wf.TargetRelLabel}: {e.Message}");
                }
            }

            var header = failed == 0
                ? $"Installed {copied} item(s) successfully for {CurrentPlatform.DisplayName}."
                : $"Installed {copied} item(s); {failed} failed. Target: {CurrentPlatform.DisplayName}.";

            EditorUtility.DisplayDialog(
                "T2FBuild CI Installer",
                header + "\n\n" + summary +
                "\nNext steps:\n" + CurrentPlatform.PostInstallNote +
                "\n\nRemember to commit the new files so CI can find them.",
                "OK");

            Refresh();
        }

        static int ResolveSavedPlatformIndex()
        {
            var saved = EditorPrefs.GetString(EditorPrefsPlatformKey, Platforms[0].Id);
            for (var i = 0; i < Platforms.Length; i++)
            {
                if (Platforms[i].Id == saved) return i;
            }
            return 0;
        }

        static void EnsureDir(string dir)
        {
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
        }

        static string PathOrDash(string path) => string.IsNullOrEmpty(path) ? "—" : path;

        class WorkflowTemplate
        {
            public string FileName;

            public string SourcePath;

            public string TargetPath;

            public string TargetRelLabel;
        }

        class CIPlatformDef
        {
            public string Id;

            public string DisplayName;

            public string TemplatesSubDir;

            public Func<string, string> ResolveProjectRelTarget;

            public string TargetLabel;

            public string OpenButtonLabel;

            public string OpenButtonRelPath;

            public string PostInstallNote;
        }
    }
}
