using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using T2FBuild.Editor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace T2FBuild.Editor.Uploaders.TencentCos
{
    [AssetBundleUploader("TencentCos")]
    public class TencentCosUploader : IAssetBundleUploader
    {
        const string ScriptRelativePath = "CI/Templates~/tools/upload-cos.py";

        const string ProjectToolsRelativePath = "tools/upload-cos.py";

        public string Name => "TencentCos";

        public Task<UploadResult> UploadAsync(UploadRequest req, CancellationToken ct)
        {
            return Task.Run(() => UploadInternal(req, ct), ct);
        }

        UploadResult UploadInternal(UploadRequest req, CancellationToken ct)
        {
            var scriptPath = LocateScript();
            if (scriptPath == null)
            {
                return new UploadResult
                {
                    Success = false,
                    Error = $"upload-cos.py not found. Copy {ScriptRelativePath} from the T2FBuild package into <project>/{ProjectToolsRelativePath}, " +
                            "or ensure the T2FBuild package is reachable on disk.",
                };
            }

            var python = LocatePython();
            if (python == null)
            {
                return new UploadResult
                {
                    Success = false,
                    Error = "Python executable not found. Tried 'python', 'python3', 'py' on PATH. " +
                            "Fix: install Python 3 (https://www.python.org/downloads/) AND restart Unity Hub so it picks up the updated PATH, " +
                            "OR set Custom Python Path in Edit > Project Settings > T2FBuild > Upload to point at python.exe directly.",
                };
            }

            var psi = new ProcessStartInfo
            {
                FileName = python,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(scriptPath),
            };
            if (python == "py")
            {
                psi.ArgumentList.Add("-3");
            }
            psi.ArgumentList.Add(scriptPath);
            psi.ArgumentList.Add(req.ManifestPath);

            var argsForLog = python == "py" ? $"-3 \"{scriptPath}\" \"{req.ManifestPath}\"" : $"\"{scriptPath}\" \"{req.ManifestPath}\"";
            Debug.Log($"[T2FBuild][TencentCos] $ {python} {argsForLog}");

            using var process = Process.Start(psi);
            if (process == null)
            {
                return new UploadResult { Success = false, Error = "Failed to start python process." };
            }

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data != null) Debug.Log($"[T2FBuild][TencentCos] {e.Data}");
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null) Debug.LogWarning($"[T2FBuild][TencentCos] {e.Data}");
            };
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            while (!process.WaitForExit(500))
            {
                if (ct.IsCancellationRequested)
                {
                    try { process.Kill(); } catch { /* ignored */ }
                    return new UploadResult { Success = false, Error = "Cancelled" };
                }
            }
            process.WaitForExit();

            return new UploadResult
            {
                Success = process.ExitCode == 0,
                Error = process.ExitCode != 0 ? $"upload-cos.py exited with code {process.ExitCode}" : null,
            };
        }

        static string LocateScript()
        {
            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            if (!string.IsNullOrEmpty(projectRoot))
            {
                var projectTool = Path.Combine(projectRoot, ProjectToolsRelativePath);
                if (File.Exists(projectTool)) return projectTool;
            }

            var packageRoot = T2FBuildPackagePath.ResolveRoot();
            if (!string.IsNullOrEmpty(packageRoot))
            {
                var pkgPath = Path.Combine(packageRoot, ScriptRelativePath);
                if (File.Exists(pkgPath)) return pkgPath;
            }

            return null;
        }

        static string LocatePython()
        {
            var custom = T2FBuildSettings.instance?.customPythonPath;
            if (!string.IsNullOrEmpty(custom))
            {
                if (File.Exists(custom)) return custom;
                Debug.LogWarning($"[T2FBuild][TencentCos] customPythonPath '{custom}' does not exist; falling back to PATH lookup.");
            }

            foreach (var name in new[] { "python", "python3", "py" })
            {
                try
                {
                    using var test = Process.Start(new ProcessStartInfo
                    {
                        FileName = name,
                        Arguments = name == "py" ? "-3 --version" : "--version",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                    });
                    if (test == null) continue;
                    if (!test.WaitForExit(2000))
                    {
                        try { test.Kill(); } catch { /* ignored */ }
                        continue;
                    }
                    if (test.ExitCode == 0) return name;
                }
                catch
                {
                    // try next
                }
            }
            return null;
        }
    }
}
