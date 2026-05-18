using System;
using System.Collections.Generic;

namespace T2FBuild.Editor
{
    [Serializable]
    public class UploadManifest
    {
        public string schema = "t2fbuild.upload-manifest.v1";

        public string timestamp;

        public string provider;

        public string target;

        public string profile;

        public string buildVersion;

        public string buildEnv;

        public string localDirectory;

        public string remotePrefix;

        public List<UploadManifestFile> files = new List<UploadManifestFile>();
    }

    [Serializable]
    public class UploadManifestFile
    {
        public string relativePath;

        public long size;

        public string sha256;
    }
}
