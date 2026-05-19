using System;
using System.IO;
using System.Threading;
using UnityEngine;

namespace T2FBuild.Editor
{
    public class UploadPlayerStep : IBuildStep
    {
        readonly string _uploaderName;

        readonly string _remotePrefixTemplate;

        readonly bool _required;

        public UploadPlayerStep(string uploaderName, string remotePrefixTemplate, bool required = false)
        {
            _uploaderName = uploaderName;
            _remotePrefixTemplate = remotePrefixTemplate;
            _required = required;
        }

        public string Name => $"Upload Player ({_uploaderName})";

        public void Execute(BuildContext ctx)
        {
            if (!IsUploadEnabled())
            {
                Debug.Log("[T2FBuild] Player upload skipped. Set T2FBUILD_UPLOAD_ENABLED=true, or enable 'Upload > Enabled By Default' in Project Settings > T2FBuild.");
                return;
            }

            var playerPath = ctx.Extras.TryGetValue("player.outputPath", out var p) ? p as string : null;
            if (string.IsNullOrEmpty(playerPath))
            {
                throw new InvalidOperationException(
                    "[T2FBuild] player.outputPath missing from BuildContext.Extras. UploadPlayerStep must run after BuildPlayerStep.");
            }
            if (!Directory.Exists(playerPath))
            {
                throw new InvalidOperationException($"[T2FBuild] Player output directory not found: {playerPath}");
            }

            IAssetBundleUploader uploader;
            try
            {
                uploader = AssetBundleUploaderRegistry.Get(_uploaderName);
            }
            catch (InvalidOperationException) when (!_required)
            {
                Debug.LogWarning($"[T2FBuild] Uploader '{_uploaderName}' not available, skipping player upload (not required).");
                return;
            }

            var remotePrefix = ResolveTemplate(_remotePrefixTemplate, ctx);
            var req = new UploadRequest
            {
                LocalDirectory = playerPath,
                RemotePrefix = remotePrefix,
                Mode = UploadMode.Full,
            };

            var result = uploader.UploadAsync(req, CancellationToken.None).GetAwaiter().GetResult();
            if (!result.Success)
            {
                throw new Exception($"[T2FBuild] Player upload failed via '{_uploaderName}': {result.Error}");
            }

            ctx.Extras["player.remotePrefix"] = remotePrefix;
            Debug.Log($"[T2FBuild] Player upload complete via '{_uploaderName}': {result.FilesUploaded} files, {result.TotalBytesUploaded} bytes, prefix={remotePrefix}");
        }

        static string ResolveTemplate(string template, BuildContext ctx)
        {
            var settings = T2FBuildSettings.instance;
            var projectId = settings != null ? settings.projectId ?? string.Empty : string.Empty;
            var projectWithSlash = string.IsNullOrEmpty(projectId) ? string.Empty : projectId + "/";
            var profile = ctx.Profile ?? string.Empty;
            var profileSuffix = string.IsNullOrEmpty(profile) ? string.Empty : "_" + profile;

            var prefix = (template ?? string.Empty)
                .Replace("{project}/", projectWithSlash)
                .Replace("{project}", projectId)
                .Replace("{target}", ctx.Target.ToString())
                .Replace("{profileSuffix}", profileSuffix)
                .Replace("{profile}", profile)
                .Replace("{env}", ctx.Env)
                .Replace("{version}", ctx.Version);
            return prefix.EndsWith("/") ? prefix : prefix + "/";
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
