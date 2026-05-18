namespace T2FBuild.Editor
{
    public interface IAssetBundleProvider
    {
        string Name { get; }

        AssetBundleBuildResult Build(BuildContext ctx);
    }
}
