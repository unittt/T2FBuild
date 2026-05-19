using System;
using System.Collections.Generic;
using System.IO;

namespace T2FBuild.Editor.Uploaders.TencentCos
{
    /// <summary>
    /// Maps file extensions to (Content-Type, Content-Encoding) for COS uploads.
    /// Ported from CI/Templates~/tools/upload-cos.py (now removed).
    /// Critical for WebGL static hosting: COS serves .br/.gz with the correct
    /// Content-Encoding so browsers decompress transparently.
    /// </summary>
    public static class CosContentMetadata
    {
        static readonly Dictionary<string, string> ContentTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { ".html", "text/html; charset=utf-8" },
            { ".htm", "text/html; charset=utf-8" },
            { ".js", "application/javascript; charset=utf-8" },
            { ".mjs", "application/javascript; charset=utf-8" },
            { ".css", "text/css; charset=utf-8" },
            { ".json", "application/json; charset=utf-8" },
            { ".wasm", "application/wasm" },
            { ".data", "application/octet-stream" },
            { ".symbols", "application/octet-stream" },
            { ".unityweb", "application/octet-stream" },
            { ".bundle", "application/octet-stream" },
            { ".png", "image/png" },
            { ".jpg", "image/jpeg" },
            { ".jpeg", "image/jpeg" },
            { ".gif", "image/gif" },
            { ".svg", "image/svg+xml" },
            { ".ico", "image/x-icon" },
            { ".txt", "text/plain; charset=utf-8" },
            { ".xml", "application/xml; charset=utf-8" },
        };

        public static (string ContentType, string ContentEncoding) Detect(string relPath)
        {
            if (string.IsNullOrEmpty(relPath)) return (null, null);
            var lower = relPath.ToLowerInvariant();

            if (lower.EndsWith(".br"))
            {
                var inner = Path.GetExtension(lower.Substring(0, lower.Length - 3));
                ContentTypes.TryGetValue(inner, out var ct);
                return (ct, "br");
            }
            if (lower.EndsWith(".gz"))
            {
                var inner = Path.GetExtension(lower.Substring(0, lower.Length - 3));
                ContentTypes.TryGetValue(inner, out var ct);
                return (ct, "gzip");
            }

            ContentTypes.TryGetValue(Path.GetExtension(lower), out var ct2);
            return (ct2, null);
        }
    }
}
