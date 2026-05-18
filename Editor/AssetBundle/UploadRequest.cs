namespace T2FBuild.Editor
{
    public class UploadRequest
    {
        public string LocalDirectory { get; set; }

        public string RemotePrefix { get; set; }

        public UploadMode Mode { get; set; } = UploadMode.Full;

        public string ManifestPath { get; set; }
    }
}
