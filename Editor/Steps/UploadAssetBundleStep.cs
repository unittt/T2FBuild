using System;
using System.IO;
using System.Threading;
using UnityEngine;

namespace T2FBuild.Editor
{
    public class UploadAssetBundleStep : IBuildStep
    {
        readonly string _uploaderName;

        readonly bool _required;

        public UploadAssetBundleStep(string uploaderName, bool required = false)
        {
            _uploaderName = uploaderName;
            _required = required;
        }

        public string Name => $"Upload Asset Bundle ({_uploaderName})";

        public void Execute(BuildContext ctx)
        {
            if (!IsUploadEnabled())
            {
                Debug.Log("[T2FBuild] Upload skipped. Set T2FBUILD_UPLOAD_ENABLED=true, or enable 'Upload > Enabled By Default' in Project Settings > T2FBuild.");
                return;
            }

            IAssetBundleUploader uploader;
            try
            {
                uploader = AssetBundleUploaderRegistry.Get(_uploaderName);
            }
            catch (InvalidOperationException) when (!_required)
            {
                Debug.LogWarning($"[T2FBuild] Uploader '{_uploaderName}' not available, skipping (not required).");
                return;
            }

            var manifestPath = ctx.Extras.TryGetValue("manifest.path", out var m) ? m as string : null;
            var localDir = ctx.Extras.TryGetValue("ab.outputDir", out var d) ? d as string : null;
            var remotePrefix = ctx.Extras.TryGetValue("manifest.remotePrefix", out var r) ? r as string : null;

            if (string.IsNullOrEmpty(manifestPath) || !File.Exists(manifestPath))
            {
                throw new InvalidOperationException(
                    "[T2FBuild] Upload manifest not found. " +
                    "UploadAssetBundleStep must run after GenerateUploadManifestStep.");
            }

            var req = new UploadRequest
            {
                LocalDirectory = localDir,
                RemotePrefix = remotePrefix,
                Mode = UploadMode.Full,
                ManifestPath = manifestPath,
            };

            var result = uploader.UploadAsync(req, CancellationToken.None).GetAwaiter().GetResult();

            if (!result.Success)
            {
                throw new Exception($"[T2FBuild] Upload failed via '{_uploaderName}': {result.Error}");
            }

            Debug.Log($"[T2FBuild] Upload complete via '{_uploaderName}': {result.FilesUploaded} files, {result.TotalBytesUploaded} bytes.");
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
