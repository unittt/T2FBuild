using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace T2FBuild.Editor.Uploaders.TencentCos
{
    [AssetBundleUploader("TencentCos")]
    public class TencentCosUploader : IAssetBundleUploader
    {
        public string Name => "TencentCos";

        public Task<UploadResult> UploadAsync(UploadRequest req, CancellationToken ct)
        {
            return Task.Run(() => UploadInternal(req, ct), ct);
        }

        static UploadResult UploadInternal(UploadRequest req, CancellationToken ct)
        {
            var creds = ReadCredentials();
            if (creds.Error != null)
            {
                return new UploadResult { Success = false, Error = creds.Error };
            }

            List<FileEntry> files;
            string source;
            try
            {
                files = ResolveFiles(req, out source);
            }
            catch (Exception e)
            {
                return new UploadResult { Success = false, Error = e.Message };
            }

            if (files.Count == 0)
            {
                Debug.Log($"[T2FBuild][TencentCos] Nothing to upload ({source}).");
                return new UploadResult { Success = true, FilesUploaded = 0, TotalBytesUploaded = 0 };
            }

            Debug.Log(
                $"[T2FBuild][TencentCos] Uploading {files.Count} files → cos://{creds.Bucket}/ ({creds.Region}); source={source}");

            using var client = new TencentCosClient(creds.SecretId, creds.SecretKey, creds.Bucket, creds.Region);
            var uploaded = 0;
            var totalBytes = 0L;
            var errors = new List<string>();

            for (var i = 0; i < files.Count; i++)
            {
                if (ct.IsCancellationRequested)
                {
                    return new UploadResult { Success = false, Error = "Cancelled" };
                }

                var f = files[i];
                var (contentType, contentEncoding) = CosContentMetadata.Detect(f.RemoteKey);

                try
                {
                    client.PutObjectAsync(f.RemoteKey, f.LocalPath, contentType, contentEncoding, ct).GetAwaiter().GetResult();
                    uploaded++;
                    totalBytes += f.Size;
                    var enc = string.IsNullOrEmpty(contentEncoding) ? string.Empty : $" [enc={contentEncoding}]";
                    Debug.Log($"[T2FBuild][TencentCos] [{i + 1}/{files.Count}] OK  {f.RemoteKey}{enc} ({f.Size} bytes)");
                }
                catch (Exception e)
                {
                    errors.Add($"{f.RemoteKey}: {e.Message}");
                    Debug.LogWarning($"[T2FBuild][TencentCos] [{i + 1}/{files.Count}] FAIL {f.RemoteKey}: {e.Message}");
                }
            }

            if (errors.Count > 0)
            {
                return new UploadResult
                {
                    Success = false,
                    Error = $"{errors.Count}/{files.Count} files failed; first error: {errors[0]}",
                    FilesUploaded = uploaded,
                    TotalBytesUploaded = totalBytes,
                };
            }

            return new UploadResult
            {
                Success = true,
                FilesUploaded = uploaded,
                TotalBytesUploaded = totalBytes,
            };
        }

        static List<FileEntry> ResolveFiles(UploadRequest req, out string source)
        {
            if (!string.IsNullOrEmpty(req.ManifestPath) && File.Exists(req.ManifestPath))
            {
                source = $"manifest {req.ManifestPath}";
                return LoadFromManifest(req.ManifestPath);
            }
            if (!string.IsNullOrEmpty(req.LocalDirectory) && Directory.Exists(req.LocalDirectory))
            {
                if (string.IsNullOrEmpty(req.RemotePrefix))
                {
                    throw new InvalidOperationException(
                        "[T2FBuild] UploadRequest.RemotePrefix is required when using LocalDirectory (no manifest).");
                }
                source = $"directory {req.LocalDirectory}";
                return LoadFromDirectory(req.LocalDirectory, req.RemotePrefix);
            }
            throw new InvalidOperationException(
                "[T2FBuild] UploadRequest must specify either a valid ManifestPath or LocalDirectory + RemotePrefix.");
        }

        static List<FileEntry> LoadFromManifest(string manifestPath)
        {
            var json = File.ReadAllText(manifestPath);
            var manifest = JsonUtility.FromJson<UploadManifest>(json);
            if (manifest == null)
            {
                throw new InvalidOperationException($"[T2FBuild] Failed to parse manifest {manifestPath}.");
            }
            var local = manifest.localDirectory ?? string.Empty;
            var remotePrefix = NormalizePrefix(manifest.remotePrefix);
            var files = new List<FileEntry>(manifest.files?.Count ?? 0);
            if (manifest.files == null) return files;
            foreach (var f in manifest.files)
            {
                if (string.IsNullOrEmpty(f.relativePath)) continue;
                var rel = f.relativePath.Replace('\\', '/');
                var localPath = Path.Combine(local, rel);
                files.Add(new FileEntry
                {
                    LocalPath = localPath,
                    RemoteKey = remotePrefix + rel,
                    Size = f.size,
                });
            }
            return files;
        }

        static List<FileEntry> LoadFromDirectory(string localDir, string remotePrefix)
        {
            var basePath = Path.GetFullPath(localDir);
            var prefix = NormalizePrefix(remotePrefix);
            var files = new List<FileEntry>();
            foreach (var full in Directory.EnumerateFiles(basePath, "*", SearchOption.AllDirectories))
            {
                var rel = Path.GetRelativePath(basePath, full).Replace('\\', '/');
                files.Add(new FileEntry
                {
                    LocalPath = full,
                    RemoteKey = prefix + rel,
                    Size = new FileInfo(full).Length,
                });
            }
            return files;
        }

        static string NormalizePrefix(string prefix)
        {
            if (string.IsNullOrEmpty(prefix)) return string.Empty;
            return prefix.EndsWith("/") ? prefix : prefix + "/";
        }

        static Credentials ReadCredentials()
        {
            var sid = Env("TENCENT_SECRET_ID");
            var sk = Env("TENCENT_SECRET_KEY");
            var bucket = Env("COS_BUCKET");
            var region = Env("COS_REGION");

            var missing = new List<string>();
            if (string.IsNullOrEmpty(sid)) missing.Add("TENCENT_SECRET_ID");
            if (string.IsNullOrEmpty(sk)) missing.Add("TENCENT_SECRET_KEY");
            if (string.IsNullOrEmpty(bucket)) missing.Add("COS_BUCKET");
            if (string.IsNullOrEmpty(region)) missing.Add("COS_REGION");
            if (missing.Count > 0)
            {
                return new Credentials
                {
                    Error =
                        $"Missing env vars: {string.Join(", ", missing)}. " +
                        "Fill them in Edit > Project Settings > T2FBuild > Secrets (Bucket/Region from Tencent COS section). " +
                        "BuildWindow auto-injects envs.yml on build; shell env vars also work as fallback.",
                };
            }
            return new Credentials { SecretId = sid, SecretKey = sk, Bucket = bucket, Region = region };
        }

        static string Env(string key)
        {
            var v = Environment.GetEnvironmentVariable(key);
            return string.IsNullOrEmpty(v) ? null : v;
        }

        class FileEntry
        {
            public string LocalPath;

            public string RemoteKey;

            public long Size;
        }

        class Credentials
        {
            public string SecretId;

            public string SecretKey;

            public string Bucket;

            public string Region;

            public string Error;
        }
    }
}
