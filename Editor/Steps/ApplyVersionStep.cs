using System;
using UnityEditor;
using UnityEngine;

namespace T2FBuild.Editor
{
    public class ApplyVersionStep : IBuildStep
    {
        public string Name => "Apply Version";

        public void Execute(BuildContext ctx)
        {
            PlayerSettings.bundleVersion = ctx.Version;

            switch (ctx.Target)
            {
                case BuildTarget.Android:
                    PlayerSettings.Android.bundleVersionCode = Math.Max(ctx.BuildNumber, 1);
                    break;
                case BuildTarget.iOS:
                    PlayerSettings.iOS.buildNumber = ctx.BuildNumber.ToString();
                    break;
            }

            Debug.Log($"[T2FBuild] Version applied: bundleVersion={ctx.Version}, buildNumber={ctx.BuildNumber}");
        }
    }
}
