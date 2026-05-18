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
            foreach (var (type, attr) in RegistryScanner.Scan<IPlatformBuilder, PlatformBuilderAttribute>())
            {
                _builders[(attr.Target, attr.Profile ?? string.Empty)] = type;
            }
        }
    }
}
