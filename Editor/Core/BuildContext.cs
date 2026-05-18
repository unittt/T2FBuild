using System;
using System.Collections.Generic;
using UnityEditor;

namespace T2FBuild.Editor
{
    public class BuildContext
    {
        public BuildTarget Target { get; set; }

        public string Profile { get; set; }

        public string Version { get; set; }

        public int BuildNumber { get; set; }

        public string Env { get; set; }

        public string OutputRoot { get; set; }

        public Dictionary<string, object> Extras { get; } = new Dictionary<string, object>();

        public static BuildContext FromEnvironment(BuildTarget target, string profile = null)
        {
            var ctx = new BuildContext
            {
                Target = target,
                Profile = profile,
                Version = GetEnv("BUILD_VERSION", "0.0.1"),
                BuildNumber = int.TryParse(GetEnv("BUILD_NUMBER", "0"), out var n) ? n : 0,
                Env = GetEnv("BUILD_ENV", "dev"),
            };
            var suffix = string.IsNullOrEmpty(profile) ? string.Empty : "_" + profile;
            ctx.OutputRoot = $"Build/{target}{suffix}/";
            return ctx;
        }

        static string GetEnv(string key, string fallback)
        {
            var v = Environment.GetEnvironmentVariable(key);
            return string.IsNullOrEmpty(v) ? fallback : v;
        }
    }
}
