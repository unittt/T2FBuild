using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;

namespace T2FBuild.Editor
{
    public static class PlatformBuilderRegistry
    {
        static Dictionary<(BuildTarget target, string profile), Type> _builders;

        public static IPlatformBuilder Get(BuildTarget target, string profile = null)
        {
            EnsureScanned();
            var key = (target, profile ?? string.Empty);
            if (!_builders.TryGetValue(key, out var type))
            {
                var profileText = string.IsNullOrEmpty(profile) ? string.Empty : $" (profile={profile})";
                throw new InvalidOperationException(
                    $"[T2FBuild] No IPlatformBuilder registered for {target}{profileText}. " +
                    "Decorate your builder class with [PlatformBuilder(target, profile)].");
            }
            return (IPlatformBuilder)Activator.CreateInstance(type);
        }

        static void EnsureScanned()
        {
            if (_builders != null) return;
            _builders = new Dictionary<(BuildTarget, string), Type>();

            var iface = typeof(IPlatformBuilder);
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
                    var attr = t.GetCustomAttribute<PlatformBuilderAttribute>();
                    if (attr == null) continue;
                    _builders[(attr.Target, attr.Profile ?? string.Empty)] = t;
                }
            }
        }
    }
}
