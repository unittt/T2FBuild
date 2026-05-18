using System;

namespace T2FBuild.Editor
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public class AssetBundleUploaderAttribute : Attribute
    {
        public string Name { get; }

        public AssetBundleUploaderAttribute(string name)
        {
            Name = name;
        }
    }
}
