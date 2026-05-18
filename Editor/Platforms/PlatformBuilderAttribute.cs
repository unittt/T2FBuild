using System;
using UnityEditor;

namespace T2FBuild.Editor
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public class PlatformBuilderAttribute : Attribute
    {
        public BuildTarget Target { get; }

        public string Profile { get; }

        public PlatformBuilderAttribute(BuildTarget target, string profile = null)
        {
            Target = target;
            Profile = profile;
        }
    }
}
