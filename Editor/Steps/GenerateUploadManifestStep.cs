using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;

namespace T2FBuild.Editor
{
    public class GenerateUploadManifestStep : IBuildStep
    {
        readonly string _remotePrefixTemplate;

        public GenerateUploadManifestStep(string remotePrefixTemplate = "ab/{target}/{env}/{version}/")
        {
            _remotePrefixTemplate = remotePrefixTemplate;
        }

        public string Name => "Generate Upload Manifest";

        public void Execute(BuildContext ctx)
        {
            var outputDir = ctx.Extras.TryGetValue("ab.outputDir", out var d) ? d as string : null;
            if (string.IsNullOrEmpty(outputDir) || !Directory.Exists(outputDir))
            {
                throw new InvalidOperationException(
                    $"[T2FBuild] AB output directory not found: '{outputDir}'. " +
                    "GenerateUploadManifestStep must run after BuildAssetBundleStep.");
            }

            var provider = ctx.Extras.TryGetValue("ab.provider", out var p) ? p as string : "Unknown";
            var abVersion = ctx.Extras.TryGetValue("ab.version", out var v) ? v as string : ctx.Version;

            var basePath = Path.GetFullPath(outputDir);
            var manifest = new UploadManifest
            {
                timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                provider = provider,
                target = ctx.Target.ToString(),
                profile = ctx.Profile,
                buildVersion = ctx.Version,
                buildEnv = ctx.Env,
                localDirectory = basePath,
                remotePrefix = ResolveRemotePrefix(_remotePrefixTemplate, ctx, abVersion),
                files = new List<UploadManifestFile>(),
            };

            foreach (var file in Directory.GetFiles(outputDir, "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(basePath, Path.GetFullPath(file)).Replace('\\', '/');
                var info = new FileInfo(file);
                manifest.files.Add(new UploadManifestFile
                {
                    relativePath = relativePath,
                    size = info.Length,
                    sha256 = ComputeSha256(file),
                });
            }

            var manifestPath = Path.Combine(ctx.OutputRoot, "upload-manifest.json");
            var manifestDir = Path.GetDirectoryName(manifestPath);
            if (!string.IsNullOrEmpty(manifestDir) && !Directory.Exists(manifestDir))
            {
                Directory.CreateDirectory(manifestDir);
            }
            File.WriteAllText(manifestPath, JsonUtility.ToJson(manifest, true));

            ctx.Extras["manifest.path"] = manifestPath;
            ctx.Extras["manifest.remotePrefix"] = manifest.remotePrefix;

            Debug.Log($"[T2FBuild] Upload manifest written: {manifestPath} ({manifest.files.Count} files)");
        }

        static string ResolveRemotePrefix(string template, BuildContext ctx, string version)
        {
            var settings = T2FBuildSettings.instance;
            var projectId = settings != null ? settings.projectId ?? string.Empty : string.Empty;
            var projectWithSlash = string.IsNullOrEmpty(projectId) ? string.Empty : projectId + "/";
            var profile = ctx.Profile ?? string.Empty;
            var profileSuffix = string.IsNullOrEmpty(profile) ? string.Empty : "_" + profile;

            return template
                .Replace("{project}/", projectWithSlash)
                .Replace("{project}", projectId)
                .Replace("{target}", ctx.Target.ToString())
                .Replace("{profileSuffix}", profileSuffix)
                .Replace("{profile}", profile)
                .Replace("{env}", ctx.Env)
                .Replace("{version}", version);
        }

        static string ComputeSha256(string path)
        {
            using var sha = SHA256.Create();
            using var stream = File.OpenRead(path);
            var hash = sha.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
        }
    }
}
