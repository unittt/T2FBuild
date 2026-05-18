using System;

namespace T2FBuild.Editor
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public class AssetBundleProviderAttribute : Attribute
    {
        public string Name { get; }

        public AssetBundleProviderAttribute(string name)
        {
            Name = name;
        }
    }
}
