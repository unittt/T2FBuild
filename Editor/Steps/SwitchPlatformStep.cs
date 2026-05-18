using System;
using UnityEditor;
using UnityEngine;

namespace T2FBuild.Editor
{
    public class SwitchPlatformStep : IBuildStep
    {
        public string Name => "Switch Platform";

        public void Execute(BuildContext ctx)
        {
            if (EditorUserBuildSettings.activeBuildTarget == ctx.Target)
            {
                Debug.Log($"[T2FBuild] Active build target already {ctx.Target}, skipping switch.");
                return;
            }

            var group = BuildPipeline.GetBuildTargetGroup(ctx.Target);
            Debug.Log($"[T2FBuild] Switching active build target to {ctx.Target}...");
            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(group, ctx.Target))
            {
                throw new Exception($"[T2FBuild] Failed to switch active build target to {ctx.Target}.");
            }
        }
    }
}
