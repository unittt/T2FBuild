using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace T2FBuild.Editor
{
    public class CITemplateInstallerWindow : EditorWindow
    {
        const string MenuPath = "Window/T2FBuild/CI Template Installer";

        const string PackageWorkflowsRelativePath = "CI/Templates~/workflows";

        const string PackageToolsRelativePath = "CI/Templates~/tools";

        const string ProjectWorkflowsRelativePath = ".github/workflows";

        const string ProjectToolsRelativePath = "tools";

        readonly List<WorkflowTemplate> _workflows = new List<WorkflowTemplate>();

        bool[] _workflowSelections = Array.Empty<bool>();

        bool _copyTools = true;

        string _packageRoot;

        string _projectRoot;

        Vector2 _scroll;

        [MenuItem(MenuPath)]
        public static void Open()
        {
            var win = GetWindow<CITemplateInstallerWindow>("T2FBuild CI Installer");
            win.minSize = new Vector2(520, 360);
        }

        void OnEnable() => Refresh();

        void Refresh()
        {
            _projectRoot = Path.GetDirectoryName(Application.dataPath);
            _packageRoot = ResolvePackageRoot();
            _workflows.Clear();

            if (string.IsNullOrEmpty(_packageRoot)) return;

            var workflowsDir = Path.Combine(_packageRoot, PackageWorkflowsRelativePath);
            if (!Directory.Exists(workflowsDir)) return;

            foreach (var path in Directory.GetFiles(workflowsDir, "*.yml").OrderBy(p => p))
            {
                _workflows.Add(new WorkflowTemplate
                {
                    FileName = Path.GetFileName(path),
                    SourcePath = path,
                    TargetPath = Path.Combine(_projectRoot, ProjectWorkflowsRelativePath, Path.GetFileName(path)),
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
                    "Could not resolve the T2FBuild package on disk. Is the package installed?",
                    MessageType.Error);
                if (GUILayout.Button("Refresh")) Refresh();
                return;
            }

            EditorGUILayout.LabelField("Package", PathOrDash(_packageRoot), EditorStyles.miniLabel);
            EditorGUILayout.LabelField("Project", PathOrDash(_projectRoot), EditorStyles.miniLabel);
            EditorGUILayout.Space();

            if (_workflows.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    $"No workflow templates found under '{PackageWorkflowsRelativePath}'.",
                    MessageType.Warning);
                if (GUILayout.Button("Refresh")) Refresh();
                return;
            }

            DrawWorkflowList();
            EditorGUILayout.Space();
            DrawToolsToggle();
            EditorGUILayout.Space();
            DrawApplyButton();
            EditorGUILayout.Space();
            DrawFooterButtons();
        }

        void DrawWorkflowList()
        {
            EditorGUILayout.LabelField("Workflows to copy into .github/workflows/", EditorStyles.boldLabel);

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
                    EditorGUILayout.LabelField(status, EditorStyles.miniLabel);
                }
            }
            EditorGUILayout.EndScrollView();
        }

        void DrawToolsToggle()
        {
            var toolsExists = Directory.Exists(Path.Combine(_projectRoot, ProjectToolsRelativePath));
            var statusText = toolsExists ? " (target exists, will overwrite)" : " (new)";

            _copyTools = EditorGUILayout.ToggleLeft(
                new GUIContent(
                    $"Also copy tools/ (upload-cos.py, requirements.txt, README.md){statusText}",
                    "Required by workflows that upload to Tencent COS. " +
                    "Commit the copied files so the CI runner can find them at <project>/tools/."),
                _copyTools);
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
                using (new EditorGUI.DisabledScope(!Directory.Exists(Path.Combine(_projectRoot, ProjectWorkflowsRelativePath))))
                {
                    if (GUILayout.Button("Open .github/workflows"))
                    {
                        EditorUtility.RevealInFinder(Path.Combine(_projectRoot, ProjectWorkflowsRelativePath));
                    }
                }
                using (new EditorGUI.DisabledScope(!Directory.Exists(Path.Combine(_projectRoot, ProjectToolsRelativePath))))
                {
                    if (GUILayout.Button("Open tools/"))
                    {
                        EditorUtility.RevealInFinder(Path.Combine(_projectRoot, ProjectToolsRelativePath));
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
            return _copyTools;
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
                    summary.AppendLine($"✓ .github/workflows/{wf.FileName}");
                }
                catch (Exception e)
                {
                    failed++;
                    summary.AppendLine($"✗ .github/workflows/{wf.FileName}: {e.Message}");
                }
            }

            if (_copyTools)
            {
                var src = Path.Combine(_packageRoot, PackageToolsRelativePath);
                var dst = Path.Combine(_projectRoot, ProjectToolsRelativePath);
                if (!Directory.Exists(src))
                {
                    failed++;
                    summary.AppendLine($"✗ tools/: source '{PackageToolsRelativePath}' not found in package");
                }
                else
                {
                    try
                    {
                        CopyDirectoryRecursive(src, dst);
                        copied++;
                        summary.AppendLine($"✓ tools/ (from {PackageToolsRelativePath})");
                    }
                    catch (Exception e)
                    {
                        failed++;
                        summary.AppendLine($"✗ tools/: {e.Message}");
                    }
                }
            }

            var header = failed == 0
                ? $"Installed {copied} item(s) successfully."
                : $"Installed {copied} item(s); {failed} failed.";

            EditorUtility.DisplayDialog(
                "T2FBuild CI Installer",
                header + "\n\n" + summary +
                "\nRemember to commit the new files so CI can find them.",
                "OK");

            Refresh();
        }

        static void CopyDirectoryRecursive(string src, string dst)
        {
            EnsureDir(dst);
            foreach (var path in Directory.GetFiles(src, "*", SearchOption.AllDirectories))
            {
                var rel = Path.GetRelativePath(src, path);
                var target = Path.Combine(dst, rel);
                EnsureDir(Path.GetDirectoryName(target));
                File.Copy(path, target, overwrite: true);
            }
        }

        static void EnsureDir(string dir)
        {
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
        }

        static string ResolvePackageRoot()
        {
            var info = PackageInfo.FindForAssembly(typeof(CITemplateInstallerWindow).Assembly);
            return info?.resolvedPath;
        }

        static string PathOrDash(string path) => string.IsNullOrEmpty(path) ? "—" : path;

        class WorkflowTemplate
        {
            public string FileName;

            public string SourcePath;

            public string TargetPath;
        }
    }
}
