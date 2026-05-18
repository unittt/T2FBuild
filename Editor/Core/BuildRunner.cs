using UnityEngine;

namespace T2FBuild.Editor
{
    public static class BuildRunner
    {
        public static void Execute(BuildContext ctx)
        {
            var builder = PlatformBuilderRegistry.Get(ctx.Target, ctx.Profile);
            var profileText = string.IsNullOrEmpty(ctx.Profile) ? "<none>" : ctx.Profile;
            Debug.Log($"[T2FBuild] Pipeline start: target={ctx.Target} profile={profileText} env={ctx.Env} version={ctx.Version}+{ctx.BuildNumber}");

            foreach (var step in builder.GetSteps(ctx))
            {
                Debug.Log($"[T2FBuild] >>> {step.Name}");
                step.Execute(ctx);
            }

            Debug.Log("[T2FBuild] Pipeline complete.");
        }
    }
}
