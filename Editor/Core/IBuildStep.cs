namespace T2FBuild.Editor
{
    public interface IBuildStep
    {
        string Name { get; }

        void Execute(BuildContext ctx);
    }
}
