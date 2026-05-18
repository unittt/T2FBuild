using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

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

            var iface = typeof(IAssetBundleProvider);
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try
                {
                    types = asm.GetTypes();
                }
                catch (ReflectionTypeLoadException e)
                {
                    types = e.Types;
                }

                foreach (var t in types)
                {
                    if (t == null || t.IsAbstract || t.IsInterface) continue;
                    if (!iface.IsAssignableFrom(t)) continue;
                    var attr = t.GetCustomAttribute<AssetBundleProviderAttribute>();
                    if (attr == null) continue;
                    _providers[attr.Name] = t;
                }
            }
        }
    }
}
