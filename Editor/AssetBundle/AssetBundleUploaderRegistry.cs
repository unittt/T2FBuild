using System;
using System.Collections.Generic;
using System.Linq;

namespace T2FBuild.Editor
{
    public static class AssetBundleUploaderRegistry
    {
        static Dictionary<string, Type> _uploaders;

        public static IAssetBundleUploader Get(string name)
        {
            EnsureScanned();
            if (!_uploaders.TryGetValue(name, out var type))
            {
                var installed = string.Join(", ", _uploaders.Keys);
                throw new InvalidOperationException(
                    $"[T2FBuild] No IAssetBundleUploader registered with name '{name}'. " +
                    $"Installed uploaders: [{installed}].");
            }
            return (IAssetBundleUploader)Activator.CreateInstance(type);
        }

        public static IEnumerable<string> GetInstalledNames()
        {
            EnsureScanned();
            return _uploaders.Keys.ToArray();
        }

        static void EnsureScanned()
        {
            if (_uploaders != null) return;
            _uploaders = new Dictionary<string, Type>();
            foreach (var (type, attr) in RegistryScanner.Scan<IAssetBundleUploader, AssetBundleUploaderAttribute>())
            {
                _uploaders[attr.Name] = type;
            }
        }
    }
}
