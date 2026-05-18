using System.Threading;
using System.Threading.Tasks;

namespace T2FBuild.Editor
{
    public interface IAssetBundleUploader
    {
        string Name { get; }

        Task<UploadResult> UploadAsync(UploadRequest req, CancellationToken ct);
    }
}
