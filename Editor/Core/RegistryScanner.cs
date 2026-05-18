using System;
using System.Collections.Generic;
using System.Reflection;

namespace T2FBuild.Editor
{
    internal static class RegistryScanner
    {
        public static IEnumerable<(Type type, TAttribute attr)> Scan<TInterface, TAttribute>()
            where TInterface : class
            where TAttribute : Attribute
        {
            var iface = typeof(TInterface);
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
                    var attr = t.GetCustomAttribute<TAttribute>();
                    if (attr == null) continue;
                    yield return (t, attr);
                }
            }
        }
    }
}
