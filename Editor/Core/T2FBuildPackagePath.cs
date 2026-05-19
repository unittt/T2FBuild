using System.IO;
using UnityEditor;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace T2FBuild.Editor
{
    public static class T2FBuildPackagePath
    {
        const string PackageJsonName = "package.json";

        const string CoreAsmdefName = "T2FBuild.Editor.asmdef";

        public static string ResolveRoot()
        {
            var info = PackageInfo.FindForAssembly(typeof(T2FBuildPackagePath).Assembly);
            if (info != null && Directory.Exists(info.resolvedPath))
            {
                return info.resolvedPath;
            }

            foreach (var guid in AssetDatabase.FindAssets("T2FBuild.Editor t:AssemblyDefinitionAsset"))
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(assetPath) || !assetPath.EndsWith("/" + CoreAsmdefName)) continue;

                var asmdefDir = Path.GetDirectoryName(Path.GetFullPath(assetPath));
                if (string.IsNullOrEmpty(asmdefDir)) continue;

                var parent = Path.GetDirectoryName(asmdefDir);
                if (!string.IsNullOrEmpty(parent) && File.Exists(Path.Combine(parent, PackageJsonName)))
                {
                    return parent;
                }
            }

            return null;
        }
    }
}
