using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor.PackageManager;
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
                    Error = "Python executable not found (tried 'python' and 'python3'). Install Python 3 and ensure it is on PATH.",
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
            psi.ArgumentList.Add(scriptPath);
            psi.ArgumentList.Add(req.ManifestPath);

            Debug.Log($"[T2FBuild][TencentCos] $ {python} \"{scriptPath}\" \"{req.ManifestPath}\"");

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

            var pkgInfo = PackageInfo.FindForAssembly(typeof(TencentCosUploader).Assembly);
            if (pkgInfo != null)
            {
                var pkgPath = Path.Combine(pkgInfo.resolvedPath, ScriptRelativePath);
                if (File.Exists(pkgPath)) return pkgPath;
            }

            return null;
        }

        static string LocatePython()
        {
            foreach (var name in new[] { "python", "python3" })
            {
                try
                {
                    using var test = Process.Start(new ProcessStartInfo
                    {
                        FileName = name,
                        Arguments = "--version",
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
