#if T2FBUILD_HAS_WECHAT
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using UnityEngine;

namespace T2FBuild.Editor.Platforms.WeChat
{
    public class UploadWeChatFirstPackageStep : IBuildStep
    {
        readonly string _uploaderName;

        public UploadWeChatFirstPackageStep(string uploaderName)
        {
            _uploaderName = uploaderName;
        }

        public string Name => $"Upload WeChat First Package ({_uploaderName})";

        public void Execute(BuildContext ctx)
        {
            var miniGameDir = ctx.Extras.TryGetValue("wechat.miniGameDir", out var d) ? d as string : null;
            var remotePrefix = ctx.Extras.TryGetValue("wechat.firstPackageRemotePrefix", out var r) ? r as string : null;
            if (string.IsNullOrEmpty(miniGameDir) || string.IsNullOrEmpty(remotePrefix))
            {
                throw new InvalidOperationException(
                    "[T2FBuild] UploadWeChatFirstPackageStep requires wechat.miniGameDir and wechat.firstPackageRemotePrefix in Extras.");
            }

            var settings = T2FBuildSettings.instance;
            var glob = settings.wechatFirstPackageGlob;
            if (string.IsNullOrEmpty(glob))
            {
                throw new InvalidOperationException(
                    "[T2FBuild] wechatFirstPackageGlob is empty. Configure it in Project Settings > T2FBuild > WeChat MiniGame.");
            }

            var matches = Directory.GetFiles(miniGameDir, glob, SearchOption.TopDirectoryOnly);
            if (matches.Length == 0)
            {
                throw new InvalidOperationException(
                    $"[T2FBuild] No files matched glob '{glob}' in {miniGameDir}. " +
                    "Inspect the WeChat export output and adjust wechatFirstPackageGlob.");
            }

            var manifest = BuildManifest(ctx, miniGameDir, matches, remotePrefix);
            var manifestPath = Path.Combine(ctx.OutputRoot, "wechat-first-package-manifest.json");
            File.WriteAllText(manifestPath, JsonUtility.ToJson(manifest, true));
            ctx.Extras["wechat.firstPackageManifestPath"] = manifestPath;
            Debug.Log($"[T2FBuild] WeChat first-package manifest: {manifestPath} ({matches.Length} files)");

            if (!IsUploadEnabled())
            {
                Debug.Log("[T2FBuild] WeChat first-package upload skipped (T2FBUILD_UPLOAD_ENABLED not set). CI should call upload-cos.py --manifest with the manifest path above.");
                return;
            }

            var uploader = AssetBundleUploaderRegistry.Get(_uploaderName);
            var req = new UploadRequest
            {
                LocalDirectory = miniGameDir,
                RemotePrefix = remotePrefix,
                Mode = UploadMode.Full,
                ManifestPath = manifestPath,
            };

            var result = uploader.UploadAsync(req, CancellationToken.None).GetAwaiter().GetResult();
            if (!result.Success)
            {
                throw new Exception($"[T2FBuild] WeChat first-package upload failed via '{_uploaderName}': {result.Error}");
            }

            Debug.Log($"[T2FBuild] WeChat first-package upload complete via '{_uploaderName}': {result.FilesUploaded} files, {result.TotalBytesUploaded} bytes.");
        }

        static UploadManifest BuildManifest(BuildContext ctx, string miniGameDir, IEnumerable<string> files, string remotePrefix)
        {
            var basePath = Path.GetFullPath(miniGameDir);
            var manifest = new UploadManifest
            {
                timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                provider = "WeChatFirstPackage",
                target = ctx.Target.ToString(),
                profile = ctx.Profile,
                buildVersion = ctx.Version,
                buildEnv = ctx.Env,
                localDirectory = basePath,
                remotePrefix = remotePrefix,
                files = new List<UploadManifestFile>(),
            };
            foreach (var file in files)
            {
                var info = new FileInfo(file);
                var rel = Path.GetRelativePath(basePath, Path.GetFullPath(file)).Replace('\\', '/');
                manifest.files.Add(new UploadManifestFile
                {
                    relativePath = rel,
                    size = info.Length,
                    sha256 = ComputeSha256(file),
                });
            }
            return manifest;
        }

        static string ComputeSha256(string path)
        {
            using var sha = SHA256.Create();
            using var stream = File.OpenRead(path);
            var hash = sha.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
        }

        static bool IsUploadEnabled()
        {
            var enabled = Environment.GetEnvironmentVariable("T2FBUILD_UPLOAD_ENABLED");
            if (enabled != null)
            {
                return enabled.Equals("true", StringComparison.OrdinalIgnoreCase) || enabled == "1";
            }
            return T2FBuildSettings.instance.uploadEnabledByDefault;
        }
    }
}
#endif
