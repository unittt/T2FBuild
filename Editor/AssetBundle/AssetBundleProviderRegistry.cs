using System;
using System.Collections.Generic;
using System.Linq;

namespace T2FBuild.Editor
{
    public static class AssetBundleProviderRegistry
    {
        static Dictionary<string, Type> _providers;

        public static IAssetBundleProvider Get(string name)
        {
            EnsureScanned();
            if (!_providers.TryGetValue(name, out var type))
            {
                var installed = string.Join(", ", _providers.Keys);
                throw new InvalidOperationException(
                    $"[T2FBuild] No IAssetBundleProvider registered with name '{name}'. " +
                    $"Installed providers: [{installed}]. " +
                    "Ensure the matching package (e.g. com.unity.addressables) is installed.");
            }
            return (IAssetBundleProvider)Activator.CreateInstance(type);
        }

        public static IEnumerable<string> GetInstalledNames()
        {
            EnsureScanned();
            return _providers.Keys.ToArray();
        }

        static void EnsureScanned()
        {
            if (_providers != null) return;
            _providers = new Dictionary<string, Type>();
            foreach (var (type, attr) in RegistryScanner.Scan<IAssetBundleProvider, AssetBundleProviderAttribute>())
            {
                _providers[attr.Name] = type;
            }
        }
    }
}
