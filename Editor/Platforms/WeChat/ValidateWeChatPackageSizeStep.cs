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
            var excluded = string.IsNullOrEmpty(glob)
                ? new HashSet<string>()
                : new HashSet<string>(
                    Directory.GetFiles(miniGameDir, glob, SearchOption.TopDirectoryOnly)
                        .Select(p => Path.GetFullPath(p)));

            var entries = new List<(string Path, long Size)>();
            long totalBytes = 0;
            foreach (var file in Directory.EnumerateFiles(miniGameDir, "*", SearchOption.AllDirectories))
            {
                var full = Path.GetFullPath(file);
                if (excluded.Contains(full)) continue;
                var size = new FileInfo(file).Length;
                entries.Add((file, size));
                totalBytes += size;
            }

            var limitBytes = (long)_limitMB * 1024 * 1024;
            var totalMB = totalBytes / (1024.0 * 1024.0);
            ctx.Extras["wechat.mainPackageBytes"] = totalBytes;

            if (totalBytes <= limitBytes)
            {
                Debug.Log($"[T2FBuild] WeChat main package OK: {totalMB:F2} MB (limit {_limitMB} MB, {entries.Count} files, excluded {excluded.Count} first-package files).");
                return;
            }

            var top = entries.OrderByDescending(e => e.Size).Take(10);
            var report = new StringBuilder();
            report.AppendLine($"[T2FBuild] WeChat main package too large: {totalMB:F2} MB > {_limitMB} MB limit.");
            report.AppendLine($"  Directory: {miniGameDir}");
            report.AppendLine($"  Excluded (uploaded to CDN): glob '{glob}', {excluded.Count} files");
            report.AppendLine("  Top 10 contributors:");
            foreach (var (path, size) in top)
            {
                var rel = Path.GetRelativePath(miniGameDir, path).Replace('\\', '/');
                report.AppendLine($"    {size,12:N0} B  {rel}");
            }
            report.AppendLine("Move heavy assets to remote AssetBundles, widen wechatFirstPackageGlob, or raise wechatMainPackageSizeLimitMB.");
            throw new InvalidOperationException(report.ToString());
        }
    }
}
#endif
