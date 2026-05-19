#if T2FBUILD_HAS_WECHAT
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace T2FBuild.Editor.Platforms.WeChat
{
    public class ValidateWeChatPackageSizeStep : IBuildStep
    {
        readonly int _limitMB;

        public ValidateWeChatPackageSizeStep(int limitMB)
        {
            _limitMB = limitMB;
        }

        public string Name => $"Validate WeChat Main Package Size (<= {_limitMB} MB)";

        public void Execute(BuildContext ctx)
        {
            var miniGameDir = ctx.Extras.TryGetValue("wechat.miniGameDir", out var d) ? d as string : null;
            if (string.IsNullOrEmpty(miniGameDir) || !Directory.Exists(miniGameDir))
            {
                throw new InvalidOperationException(
                    "[T2FBuild] wechat.miniGameDir not found. ValidateWeChatPackageSizeStep must run after RunWeChatExportStep.");
            }

            var glob = T2FBuildSettings.instance.wechatFirstPackageGlob ?? string.Empty;
            var globExcluded = string.IsNullOrEmpty(glob)
                ? new HashSet<string>()
                : new HashSet<string>(
                    Directory.GetFiles(miniGameDir, glob, SearchOption.TopDirectoryOnly)
                        .Select(p => Path.GetFullPath(p)));

            var subPackageRoots = ReadSubPackageRoots(miniGameDir);
            var dirNormalized = Path.GetFullPath(miniGameDir).Replace('\\', '/').TrimEnd('/') + "/";

            var mainEntries = new List<(string Path, long Size)>();
            var subPackageBytes = 0L;
            var subPackageCount = 0;
            long totalBytes = 0;

            foreach (var file in Directory.EnumerateFiles(miniGameDir, "*", SearchOption.AllDirectories))
            {
                var full = Path.GetFullPath(file);
                if (globExcluded.Contains(full)) continue;

                var size = new FileInfo(file).Length;
                var relForward = full.Replace('\\', '/');
                if (relForward.StartsWith(dirNormalized, StringComparison.OrdinalIgnoreCase))
                {
                    relForward = relForward.Substring(dirNormalized.Length);
                }

                if (subPackageRoots.Any(root => relForward.StartsWith(root, StringComparison.OrdinalIgnoreCase)))
                {
                    subPackageBytes += size;
                    subPackageCount++;
                    continue;
                }

                mainEntries.Add((file, size));
                totalBytes += size;
            }

            var limitBytes = (long)_limitMB * 1024 * 1024;
            var totalMB = totalBytes / (1024.0 * 1024.0);
            ctx.Extras["wechat.mainPackageBytes"] = totalBytes;

            if (totalBytes <= limitBytes)
            {
                Debug.Log(
                    $"[T2FBuild] WeChat main package OK: {totalMB:F2} MB (limit {_limitMB} MB, {mainEntries.Count} main files; " +
                    $"excluded {subPackageCount} subPackage files / {subPackageBytes / 1024.0 / 1024.0:F2} MB, " +
                    $"{globExcluded.Count} first-package CDN files).");
                return;
            }

            var top = mainEntries.OrderByDescending(e => e.Size).Take(10);
            var report = new StringBuilder();
            report.AppendLine($"[T2FBuild] WeChat main package too large: {totalMB:F2} MB > {_limitMB} MB limit.");
            report.AppendLine($"  Directory: {miniGameDir}");
            report.AppendLine($"  Excluded — subPackages from game.json: {subPackageRoots.Count} roots, {subPackageCount} files");
            report.AppendLine($"  Excluded — CDN glob '{glob}': {globExcluded.Count} files");
            report.AppendLine("  Top 10 main-package contributors:");
            foreach (var (path, size) in top)
            {
                var rel = Path.GetRelativePath(miniGameDir, path).Replace('\\', '/');
                report.AppendLine($"    {size,12:N0} B  {rel}");
            }
            report.AppendLine("Options:");
            report.AppendLine("  - Add a 'subPackages' entry to game.json containing the heavy directory (WeChat runtime will download on demand).");
            report.AppendLine("  - Widen wechatFirstPackageGlob to upload heavy files to your own CDN.");
            report.AppendLine("  - Raise wechatMainPackageSizeLimitMB if the limit is wrong for your case.");
            throw new InvalidOperationException(report.ToString());
        }

        [Serializable]
        class GameJson
        {
            public SubPackage[] subPackages;
        }

        [Serializable]
        class SubPackage
        {
            public string root;

            public string name;
        }

        static HashSet<string> ReadSubPackageRoots(string miniGameDir)
        {
            var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var gameJsonPath = Path.Combine(miniGameDir, "game.json");
            if (!File.Exists(gameJsonPath)) return roots;

            try
            {
                var text = File.ReadAllText(gameJsonPath);
                var parsed = JsonUtility.FromJson<GameJson>(text);
                if (parsed?.subPackages == null) return roots;
                foreach (var sp in parsed.subPackages)
                {
                    if (string.IsNullOrEmpty(sp?.root)) continue;
                    var normalized = sp.root.Replace('\\', '/').TrimStart('/').TrimEnd('/');
                    if (normalized.Length == 0) continue;
                    roots.Add(normalized + "/");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[T2FBuild] Failed to parse subPackages from game.json: {e.Message}. Main-package size check will treat all files as main package.");
            }

            return roots;
        }
    }
}
#endif
