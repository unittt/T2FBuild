using UnityEditor;

namespace T2FBuild.Editor
{
    public static class BuildEntry
    {
        public static void BuildAndroid() => Run(BuildTarget.Android);

        public static void BuildIOS() => Run(BuildTarget.iOS);

        public static void BuildWebGL() => Run(BuildTarget.WebGL);

        public static void BuildWeChat() => Run(BuildTarget.WebGL, "wechat");

        static void Run(BuildTarget target, string profile = null)
        {
            var ctx = BuildContext.FromEnvironment(target, profile);
            BuildRunner.Execute(ctx);
        }
    }
}
