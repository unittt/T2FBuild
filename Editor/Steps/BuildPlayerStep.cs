using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace T2FBuild.Editor
{
    public class BuildPlayerStep : IBuildStep
    {
        public string Name => "Build Player";

        public void Execute(BuildContext ctx)
        {
            var scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                throw new InvalidOperationException("[T2FBuild] No enabled scenes in EditorBuildSettings. Add at least one scene to Build Settings.");
            }

            var locationPath = ResolveLocationPath(ctx);
            EnsureParentDirectory(locationPath);

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = locationPath,
                target = ctx.Target,
                targetGroup = BuildPipeline.GetBuildTargetGroup(ctx.Target),
                options = BuildOptions.None,
            };

            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            if (summary.result != BuildResult.Succeeded)
            {
                throw new Exception($"[T2FBuild] Player build failed: result={summary.result}, errors={summary.totalErrors}");
            }

            ctx.Extras["player.outputPath"] = locationPath;
            Debug.Log($"[T2FBuild] Player built: {locationPath} (size={summary.totalSize} bytes, time={summary.totalTime})");
        }

        static string ResolveLocationPath(BuildContext ctx)
        {
            switch (ctx.Target)
            {
                case BuildTarget.Android:
                    return Path.Combine(ctx.OutputRoot, "player.apk");
                case BuildTarget.StandaloneWindows:
                case BuildTarget.StandaloneWindows64:
                    return Path.Combine(ctx.OutputRoot, "Player", "Game.exe");
                case BuildTarget.iOS:
                case BuildTarget.WebGL:
                case BuildTarget.StandaloneOSX:
                case BuildTarget.StandaloneLinux64:
                default:
                    return Path.Combine(ctx.OutputRoot, "Player");
            }
        }

        static void EnsureParentDirectory(string locationPath)
        {
            var dir = Path.GetDirectoryName(locationPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
        }
    }
}
