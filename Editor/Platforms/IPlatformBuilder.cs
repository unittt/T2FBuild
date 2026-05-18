using System.Collections.Generic;

namespace T2FBuild.Editor
{
    public interface IPlatformBuilder
    {
        IEnumerable<IBuildStep> GetSteps(BuildContext ctx);
    }
}
