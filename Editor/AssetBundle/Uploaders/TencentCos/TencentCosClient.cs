using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace T2FBuild.Editor.Uploaders.TencentCos
{
    /// <summary>
    /// Minimal Tencent COS V5 client — PUT-object only.
    /// Implements the COS V5 signature algorithm:
    /// https://cloud.tencent.com/document/product/436/7778
    /// </summary>
    public class TencentCosClient : IDisposable
    {
        readonly string _secretId;

        readonly string _secretKey;

        readonly string _bucket;

        readonly string _region;

        readonly HttpClient _http;

        public TencentCosClient(string secretId, string secretKey, string bucket, string region)
        {
            _secretId = secretId ?? throw new ArgumentNullException(nameof(secretId));
            _secretKey = secretKey ?? throw new ArgumentNullException(nameof(secretKey));
            _bucket = bucket ?? throw new ArgumentNullException(nameof(bucket));
            _region = region ?? throw new ArgumentNullException(nameof(region));
            _http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        }

        public string BuildObjectUrl(string objectKey)
        {
            var encoded = string.Join("/", (objectKey ?? string.Empty).Split('/').Select(EncodeSegment));
            return $"https://{_bucket}.cos.{_region}.myqcloud.com/{encoded}";
        }

        public async Task PutObjectAsync(string objectKey, string localFile, string contentType, string contentEncoding, CancellationToken ct)
        {
            if (!File.Exists(localFile)) throw new FileNotFoundException(localFile);

            var host = $"{_bucket}.cos.{_region}.myqcloud.com";
            var normalizedKey = (objectKey ?? string.Empty).Replace('\\', '/');
            // Signature uses RAW (URL-decoded) pathname; request URL uses encoded pathname.
            // COS service decodes the request path before signing on its end, so client must
            // sign against the raw form to match.
            var pathnameForSign = "/" + normalizedKey;
            var pathnameForUrl = "/" + string.Join("/", normalizedKey.Split('/').Select(EncodeSegment));
            var url = $"https://{host}{pathnameForUrl}";

            using var stream = new FileStream(localFile, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 81920, useAsync: true);
            using var content = new StreamContent(stream);
            if (!string.IsNullOrEmpty(contentType))
            {
                content.Headers.TryAddWithoutValidation("Content-Type", contentType);
            }
            if (!string.IsNullOrEmpty(contentEncoding))
            {
                content.Headers.TryAddWithoutValidation("Content-Encoding", contentEncoding);
            }

            using var request = new HttpRequestMessage(HttpMethod.Put, url) { Content = content };

            // Sign host header only — content-type/encoding are sent but not signed (saves complexity, COS allows this).
            var signedHeaders = new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                { "host", host },
            };
            var authorization = SignV5("put", pathnameForSign, new SortedDictionary<string, string>(StringComparer.Ordinal), signedHeaders);
            request.Headers.TryAddWithoutValidation("Authorization", authorization);

            using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                throw new Exception($"COS PUT {url} returned {(int)response.StatusCode} {response.ReasonPhrase}: {body}");
            }
        }

        string SignV5(string method, string pathname, SortedDictionary<string, string> queryParams, SortedDictionary<string, string> headers)
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var keyTime = $"{now};{now + 600}";

            var signKey = HmacSha1Hex(_secretKey, keyTime);

            var paramKeys = string.Join(";", queryParams.Keys.Select(k => k.ToLowerInvariant()));
            var paramStr = string.Join("&", queryParams.Select(kv => $"{UrlEncodeStrict(kv.Key.ToLowerInvariant())}={UrlEncodeStrict(kv.Value ?? string.Empty)}"));

            var headerKeys = string.Join(";", headers.Keys.Select(k => k.ToLowerInvariant()));
            var headerStr = string.Join("&", headers.Select(kv => $"{UrlEncodeStrict(kv.Key.ToLowerInvariant())}={UrlEncodeStrict(kv.Value ?? string.Empty)}"));

            var httpString = $"{method.ToLowerInvariant()}\n{pathname}\n{paramStr}\n{headerStr}\n";
            var sha1Http = Sha1Hex(httpString);

            var stringToSign = $"sha1\n{keyTime}\n{sha1Http}\n";
            var signature = HmacSha1Hex(signKey, stringToSign);

            return
                $"q-sign-algorithm=sha1" +
                $"&q-ak={_secretId}" +
                $"&q-sign-time={keyTime}" +
                $"&q-key-time={keyTime}" +
                $"&q-header-list={headerKeys}" +
                $"&q-url-param-list={paramKeys}" +
                $"&q-signature={signature}";
        }

        static string HmacSha1Hex(string key, string data)
        {
            using var hmac = new HMACSHA1(Encoding.UTF8.GetBytes(key));
            return ToHex(hmac.ComputeHash(Encoding.UTF8.GetBytes(data)));
        }

        static string Sha1Hex(string data)
        {
            using var sha = SHA1.Create();
            return ToHex(sha.ComputeHash(Encoding.UTF8.GetBytes(data)));
        }

        static string ToHex(byte[] bytes)
        {
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (var b in bytes) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        static string UrlEncodeStrict(string input)
        {
            return Uri.EscapeDataString(input ?? string.Empty);
        }

        static string EncodeSegment(string segment)
        {
            return Uri.EscapeDataString(segment ?? string.Empty);
        }

        public void Dispose()
        {
            _http?.Dispose();
        }
    }
}
