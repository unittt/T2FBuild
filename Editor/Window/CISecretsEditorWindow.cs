using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace T2FBuild.Editor
{
    public class CISecretsEditorWindow : EditorWindow
    {
        const string MenuPath = "Window/T2FBuild/CI Secrets Editor";

        const string DefaultEnvsFileName = "envs.yml";

        const string KeyLicense = "UNITY_LICENSE_BASE64";

        const string KeySecretId = "TENCENT_SECRET_ID";

        const string KeySecretKey = "TENCENT_SECRET_KEY";

        const string KeyBucket = "COS_BUCKET";

        const string KeyRegion = "COS_REGION";

        string _envsPath = string.Empty;

        bool _envsExists;

        string _envsLoadStatus = string.Empty;

        MessageType _envsLoadStatusType = MessageType.None;

        string _ulfPath = string.Empty;

        string _licenseBase64 = string.Empty;

        bool _licenseLoadedFromEnvs;

        string _ulfStatus = string.Empty;

        MessageType _ulfStatusType = MessageType.None;

        string _secretId = string.Empty;

        string _secretKey = string.Empty;

        string _bucket = string.Empty;

        string _region = string.Empty;

        string _bucketHint = string.Empty;

        string _regionHint = string.Empty;

        string _writeStatus = string.Empty;

        MessageType _writeStatusType = MessageType.None;

        Vector2 _scroll;

        [MenuItem(MenuPath)]
        public static void Open()
        {
            var win = GetWindow<CISecretsEditorWindow>("CI Secrets Editor");
            win.minSize = new Vector2(580, 520);
        }

        void OnEnable()
        {
            ResolveEnvsPath();
            ResolveHints();
            _ulfPath = DetectDefaultUlfPath();
            ReloadFromEnvs();
            EncodeFromUlf();
        }

        void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.LabelField("CI Secrets Editor", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUILayout.HelpBox(
                "Edit envs.yml (Unity license + Tencent COS credentials) for CNB pipeline. " +
                "Values are NEVER persisted between sessions — they live only in this window until you click Write. " +
                "Other custom fields in envs.yml are preserved on write.",
                MessageType.Info);
            EditorGUILayout.Space();

            DrawEnvsFile();
            EditorGUILayout.Space();
            DrawLicenseSection();
            EditorGUILayout.Space();
            DrawCosSection();
            EditorGUILayout.Space();
            DrawActions();
            EditorGUILayout.Space();
            DrawSecurityNote();

            EditorGUILayout.EndScrollView();
        }

        void DrawEnvsFile()
        {
            EditorGUILayout.LabelField("envs.yml", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Path", GUILayout.Width(70));
                EditorGUI.BeginChangeCheck();
                var newPath = EditorGUILayout.TextField(_envsPath);
                if (EditorGUI.EndChangeCheck())
                {
                    _envsPath = newPath;
                }
                if (GUILayout.Button("Browse...", GUILayout.Width(80)))
                {
                    var start = string.IsNullOrEmpty(_envsPath) ? string.Empty : Path.GetDirectoryName(_envsPath);
                    var picked = EditorUtility.OpenFilePanel("Select envs.yml", start, "yml");
                    if (!string.IsNullOrEmpty(picked))
                    {
                        _envsPath = picked;
                        ReloadFromEnvs();
                    }
                }
                if (GUILayout.Button("Reload", GUILayout.Width(80)))
                {
                    ReloadFromEnvs();
                }
            }
            if (!string.IsNullOrEmpty(_envsLoadStatus))
            {
                EditorGUILayout.HelpBox(_envsLoadStatus, _envsLoadStatusType);
            }
        }

        void DrawLicenseSection()
        {
            EditorGUILayout.LabelField("Unity License", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(".ulf Path", GUILayout.Width(70));
                EditorGUI.BeginChangeCheck();
                var newPath = EditorGUILayout.TextField(_ulfPath);
                if (EditorGUI.EndChangeCheck())
                {
                    _ulfPath = newPath;
                    EncodeFromUlf();
                }
                if (GUILayout.Button("Browse...", GUILayout.Width(80)))
                {
                    var start = string.IsNullOrEmpty(_ulfPath) ? string.Empty : Path.GetDirectoryName(_ulfPath);
                    var picked = EditorUtility.OpenFilePanel("Select Unity_lic.ulf", start, "ulf");
                    if (!string.IsNullOrEmpty(picked))
                    {
                        _ulfPath = picked;
                        EncodeFromUlf();
                    }
                }
                if (GUILayout.Button("Auto-Detect", GUILayout.Width(90)))
                {
                    _ulfPath = DetectDefaultUlfPath();
                    EncodeFromUlf();
                }
            }
            if (!string.IsNullOrEmpty(_ulfStatus))
            {
                EditorGUILayout.HelpBox(_ulfStatus, _ulfStatusType);
            }
        }

        void DrawCosSection()
        {
            EditorGUILayout.LabelField("Tencent COS", EditorStyles.boldLabel);

            if (string.IsNullOrEmpty(_bucketHint) && string.IsNullOrEmpty(_regionHint))
            {
                EditorGUILayout.HelpBox(
                    "Tip: fill Bucket / Region in Edit > Project Settings > T2FBuild > Tencent COS to enable hint buttons here.",
                    MessageType.None);
            }

            _secretId = EditorGUILayout.TextField(
                new GUIContent("Secret ID", "From https://console.cloud.tencent.com/cam/capi"),
                _secretId);

            _secretKey = EditorGUILayout.PasswordField(
                new GUIContent("Secret Key", "From CAM. Displayed as ******** but copyable from this field."),
                _secretKey);

            DrawHintedTextField("Bucket", "Public identifier of the COS bucket. Hint comes from Project Settings > T2FBuild > Tencent COS > Bucket.", ref _bucket, _bucketHint);
            DrawHintedTextField("Region", "Public identifier of the COS region (e.g. ap-shanghai). Hint comes from Project Settings > T2FBuild > Tencent COS > Region.", ref _region, _regionHint);
        }

        void DrawHintedTextField(string label, string tooltip, ref string value, string hint)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                value = EditorGUILayout.TextField(new GUIContent(label, tooltip), value);
                if (!string.IsNullOrEmpty(hint))
                {
                    using (new EditorGUI.DisabledScope(value == hint))
                    {
                        if (GUILayout.Button($"Use \"{Truncate(hint, 24)}\"", GUILayout.Width(180)))
                        {
                            value = hint;
                            GUI.FocusControl(null);
                        }
                    }
                }
            }
        }

        void DrawActions()
        {
            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(_licenseBase64)))
            {
                if (GUILayout.Button("Copy License Base64 to Clipboard", GUILayout.Height(28)))
                {
                    EditorGUIUtility.systemCopyBuffer = _licenseBase64;
                    _writeStatus = $"Copied license ({_licenseBase64.Length} chars) to clipboard.\n" +
                                   "⚠ Clear clipboard after use.";
                    _writeStatusType = MessageType.Info;
                }
            }

            using (new EditorGUI.DisabledScope(!HasAnyValueToWrite()))
            {
                if (GUILayout.Button("Write All to envs.yml", GUILayout.Height(34)))
                {
                    WriteAll();
                }
            }

            if (!string.IsNullOrEmpty(_writeStatus))
            {
                EditorGUILayout.HelpBox(_writeStatus, _writeStatusType);
            }
        }

        void DrawSecurityNote()
        {
            EditorGUILayout.LabelField("Security", EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField(
                "• Values typed here are NOT cached. Closing this window discards anything not written.",
                EditorStyles.miniLabel);
            EditorGUILayout.LabelField(
                "• Secret Key field is masked but its underlying value can be Ctrl+C copied — treat the screen as sensitive.",
                EditorStyles.miniLabel);
            EditorGUILayout.LabelField(
                "• envs.yml must live in a private repo. See T2FBuild/docs/cnb-wechat-setup.md §3.",
                EditorStyles.miniLabel);
        }

        bool HasAnyValueToWrite()
        {
            return !string.IsNullOrEmpty(_licenseBase64)
                || !string.IsNullOrEmpty(_secretId)
                || !string.IsNullOrEmpty(_secretKey)
                || !string.IsNullOrEmpty(_bucket)
                || !string.IsNullOrEmpty(_region);
        }

        void ResolveEnvsPath()
        {
            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            _envsPath = string.IsNullOrEmpty(projectRoot)
                ? string.Empty
                : Path.Combine(projectRoot, DefaultEnvsFileName).Replace('\\', '/');
        }

        void ResolveHints()
        {
            var settings = T2FBuildSettings.instance;
            _bucketHint = settings != null ? settings.tencentCosBucket ?? string.Empty : string.Empty;
            _regionHint = settings != null ? settings.tencentCosRegion ?? string.Empty : string.Empty;
        }

        void ReloadFromEnvs()
        {
            _envsExists = !string.IsNullOrEmpty(_envsPath) && File.Exists(_envsPath);
            if (!_envsExists)
            {
                _envsLoadStatus = $"envs.yml not found. Will be created on Write.";
                _envsLoadStatusType = MessageType.Warning;
                if (string.IsNullOrEmpty(_bucket) && !string.IsNullOrEmpty(_bucketHint)) _bucket = _bucketHint;
                if (string.IsNullOrEmpty(_region) && !string.IsNullOrEmpty(_regionHint)) _region = _regionHint;
                return;
            }

            string content;
            try
            {
                content = File.ReadAllText(_envsPath);
            }
            catch (Exception e)
            {
                _envsLoadStatus = $"Failed to read envs.yml: {e.Message}";
                _envsLoadStatusType = MessageType.Error;
                return;
            }

            var fields = ParseEnvs(content);
            var lookup = fields.ToDictionary(f => f.Key, f => f);

            if (lookup.TryGetValue(KeyLicense, out var lf) && !string.IsNullOrWhiteSpace(lf.Value) && !IsPlaceholder(lf.Value))
            {
                _licenseBase64 = lf.Value.Replace("\n", "").Replace("\r", "").Trim();
                _licenseLoadedFromEnvs = true;
            }
            _secretId = lookup.TryGetValue(KeySecretId, out var sid) && !IsPlaceholder(sid.Value) ? sid.Value : string.Empty;
            _secretKey = lookup.TryGetValue(KeySecretKey, out var sk) && !IsPlaceholder(sk.Value) ? sk.Value : string.Empty;
            _bucket = lookup.TryGetValue(KeyBucket, out var b) && !IsPlaceholder(b.Value) ? b.Value : (!string.IsNullOrEmpty(_bucketHint) ? _bucketHint : string.Empty);
            _region = lookup.TryGetValue(KeyRegion, out var r) && !IsPlaceholder(r.Value) ? r.Value : (!string.IsNullOrEmpty(_regionHint) ? _regionHint : string.Empty);

            var presentCount = new[] { KeyLicense, KeySecretId, KeySecretKey, KeyBucket, KeyRegion }
                .Count(k => lookup.TryGetValue(k, out var f) && !string.IsNullOrWhiteSpace(f.Value) && !IsPlaceholder(f.Value));

            _envsLoadStatus = $"Loaded envs.yml ({fields.Count} env fields, {presentCount}/5 known fields populated).";
            _envsLoadStatusType = MessageType.None;
        }

        static bool IsPlaceholder(string value)
        {
            return value != null && value.StartsWith("REPLACE_WITH_", StringComparison.OrdinalIgnoreCase);
        }

        void EncodeFromUlf()
        {
            if (string.IsNullOrEmpty(_ulfPath))
            {
                if (!_licenseLoadedFromEnvs)
                {
                    _ulfStatus = "No .ulf path. Click Auto-Detect / Browse, or rely on the license loaded from envs.yml (if any).";
                    _ulfStatusType = MessageType.Warning;
                }
                return;
            }

            if (!File.Exists(_ulfPath))
            {
                _ulfStatus = _licenseLoadedFromEnvs
                    ? $".ulf not found at:\n{_ulfPath}\n\nKeeping license loaded from envs.yml ({_licenseBase64.Length} chars)."
                    : $".ulf not found at:\n{_ulfPath}\n\nLocate manually or activate Unity License first.";
                _ulfStatusType = MessageType.Warning;
                return;
            }

            try
            {
                var bytes = File.ReadAllBytes(_ulfPath);
                _licenseBase64 = Convert.ToBase64String(bytes);
                _licenseLoadedFromEnvs = false;
                _ulfStatus = $"Encoded .ulf: {bytes.Length} bytes → base64 {_licenseBase64.Length} chars. Ready.";
                _ulfStatusType = MessageType.None;
            }
            catch (Exception e)
            {
                _ulfStatus = $"Failed to read .ulf: {e.Message}";
                _ulfStatusType = MessageType.Error;
            }
        }

        void WriteAll()
        {
            if (string.IsNullOrEmpty(_envsPath))
            {
                _writeStatus = "envs.yml path is empty.";
                _writeStatusType = MessageType.Error;
                return;
            }

            string content;
            try
            {
                content = File.Exists(_envsPath) ? File.ReadAllText(_envsPath) : string.Empty;
            }
            catch (Exception e)
            {
                _writeStatus = $"Failed to read envs.yml: {e.Message}";
                _writeStatusType = MessageType.Error;
                return;
            }

            var updates = new Dictionary<string, EnvField>();
            if (!string.IsNullOrEmpty(_licenseBase64)) updates[KeyLicense] = new EnvField { Key = KeyLicense, Value = _licenseBase64, IsBlock = true };
            if (!string.IsNullOrEmpty(_secretId)) updates[KeySecretId] = new EnvField { Key = KeySecretId, Value = _secretId };
            if (!string.IsNullOrEmpty(_secretKey)) updates[KeySecretKey] = new EnvField { Key = KeySecretKey, Value = _secretKey };
            if (!string.IsNullOrEmpty(_bucket)) updates[KeyBucket] = new EnvField { Key = KeyBucket, Value = _bucket };
            if (!string.IsNullOrEmpty(_region)) updates[KeyRegion] = new EnvField { Key = KeyRegion, Value = _region };

            if (updates.Count == 0)
            {
                _writeStatus = "Nothing to write — all fields are empty.";
                _writeStatusType = MessageType.Warning;
                return;
            }

            string updated;
            try
            {
                updated = MergeEnvs(content, updates);
            }
            catch (Exception e)
            {
                _writeStatus = $"Failed to merge envs.yml: {e.Message}";
                _writeStatusType = MessageType.Error;
                return;
            }

            try
            {
                var dir = Path.GetDirectoryName(_envsPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(_envsPath, updated);
                _writeStatus = $"Wrote {updates.Count} field(s) to {Path.GetFileName(_envsPath)}.\n" +
                               "⚠ envs.yml now holds real secrets. Confirm repo is private, then commit.";
                _writeStatusType = MessageType.Info;
                AssetDatabase.Refresh();
                ReloadFromEnvs();
            }
            catch (Exception e)
            {
                _writeStatus = $"Failed to write envs.yml: {e.Message}";
                _writeStatusType = MessageType.Error;
            }
        }

        // ---------- YAML-ish parse / merge ----------

        class EnvField
        {
            public string Key;

            public string Value;

            public bool IsBlock;
        }

        static List<EnvField> ParseEnvs(string content)
        {
            var fields = new List<EnvField>();
            if (string.IsNullOrEmpty(content)) return fields;
            var lines = content.Split('\n').Select(l => l.TrimEnd('\r')).ToArray();
            var inEnv = false;

            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                var indent = CountLeadingSpaces(line);
                var trimmed = line.TrimStart();

                if (!inEnv)
                {
                    if (indent == 0 && trimmed.StartsWith("env:")) inEnv = true;
                    continue;
                }

                if (indent == 0 && trimmed.Length > 0)
                {
                    inEnv = false;
                    continue;
                }

                if (indent != 2 || trimmed.StartsWith("#") || trimmed.Length == 0) continue;

                var colon = trimmed.IndexOf(':');
                if (colon <= 0) continue;

                var key = trimmed.Substring(0, colon).Trim();
                var rest = trimmed.Substring(colon + 1).TrimStart();

                if (IsBlockMarker(rest))
                {
                    var pieces = new List<string>();
                    var j = i + 1;
                    while (j < lines.Length)
                    {
                        var bline = lines[j];
                        if (bline.Trim().Length == 0) { j++; continue; }
                        if (CountLeadingSpaces(bline) <= indent) break;
                        pieces.Add(bline.TrimStart());
                        j++;
                    }
                    fields.Add(new EnvField { Key = key, Value = string.Join("\n", pieces), IsBlock = true });
                    i = j - 1;
                }
                else
                {
                    var commentIdx = rest.IndexOf(" #", StringComparison.Ordinal);
                    if (commentIdx > 0) rest = rest.Substring(0, commentIdx).TrimEnd();
                    fields.Add(new EnvField { Key = key, Value = rest, IsBlock = false });
                }
            }

            return fields;
        }

        static string MergeEnvs(string original, Dictionary<string, EnvField> updates)
        {
            var newline = original.Contains("\r\n") ? "\r\n" : "\n";
            var lines = string.IsNullOrEmpty(original)
                ? new List<string>()
                : original.Split('\n').Select(l => l.TrimEnd('\r')).ToList();

            var output = new List<string>();
            var found = new HashSet<string>();
            var inEnv = false;
            var envSeen = false;

            for (var i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                var indent = CountLeadingSpaces(line);
                var trimmed = line.TrimStart();

                if (!inEnv)
                {
                    output.Add(line);
                    if (indent == 0 && trimmed.StartsWith("env:"))
                    {
                        inEnv = true;
                        envSeen = true;
                    }
                    continue;
                }

                if (indent == 0 && trimmed.Length > 0)
                {
                    foreach (var key in updates.Keys.Where(k => !found.Contains(k)))
                    {
                        AppendFieldLines(output, updates[key]);
                    }
                    output.Add(line);
                    inEnv = false;
                    continue;
                }

                if (indent != 2 || trimmed.StartsWith("#") || trimmed.Length == 0)
                {
                    output.Add(line);
                    continue;
                }

                var colon = trimmed.IndexOf(':');
                if (colon <= 0) { output.Add(line); continue; }

                var key2 = trimmed.Substring(0, colon).Trim();
                var rest = trimmed.Substring(colon + 1).TrimStart();
                var isBlock = IsBlockMarker(rest);

                var valueEnd = i + 1;
                if (isBlock)
                {
                    while (valueEnd < lines.Count)
                    {
                        var vline = lines[valueEnd];
                        if (vline.Trim().Length == 0) { valueEnd++; continue; }
                        if (CountLeadingSpaces(vline) <= indent) break;
                        valueEnd++;
                    }
                }

                if (updates.TryGetValue(key2, out var newField))
                {
                    AppendFieldLines(output, newField);
                    found.Add(key2);
                    i = valueEnd - 1;
                }
                else
                {
                    for (var k = i; k < valueEnd; k++) output.Add(lines[k]);
                    i = valueEnd - 1;
                }
            }

            if (inEnv)
            {
                foreach (var key in updates.Keys.Where(k => !found.Contains(k)))
                {
                    AppendFieldLines(output, updates[key]);
                }
            }

            if (!envSeen)
            {
                if (output.Count > 0 && output[output.Count - 1].Length > 0) output.Add(string.Empty);
                output.Add("env:");
                foreach (var f in updates.Values)
                {
                    AppendFieldLines(output, f);
                }
            }

            var joined = string.Join(newline, output);
            if (!joined.EndsWith(newline)) joined += newline;
            return joined;
        }

        static void AppendFieldLines(List<string> output, EnvField f)
        {
            if (f.IsBlock)
            {
                output.Add($"  {f.Key}: |");
                foreach (var piece in (f.Value ?? string.Empty).Split('\n'))
                {
                    output.Add("    " + piece);
                }
            }
            else
            {
                output.Add($"  {f.Key}: {f.Value}");
            }
        }

        static bool IsBlockMarker(string s)
        {
            return s == "|" || s == "|+" || s == "|-" || s == ">" || s == ">+" || s == ">-";
        }

        static int CountLeadingSpaces(string line)
        {
            var count = 0;
            foreach (var c in line)
            {
                if (c == ' ') count++;
                else break;
            }
            return count;
        }

        static string Truncate(string s, int max)
        {
            return s.Length <= max ? s : s.Substring(0, max - 1) + "…";
        }

        static string DetectDefaultUlfPath()
        {
            var candidates = new List<string>
            {
                @"C:\ProgramData\Unity\Unity_lic.ulf",
                "/Library/Application Support/Unity/Unity_lic.ulf",
                "/var/lib/unity/Unity_lic.ulf",
            };
            var home = Environment.GetEnvironmentVariable("HOME");
            if (!string.IsNullOrEmpty(home))
            {
                candidates.Add(Path.Combine(home, ".local/share/unity3d/Unity/Unity_lic.ulf"));
            }

            foreach (var path in candidates)
            {
                if (File.Exists(path)) return path;
            }

            return Application.platform switch
            {
                RuntimePlatform.WindowsEditor => candidates[0],
                RuntimePlatform.OSXEditor => candidates[1],
                _ => candidates[2],
            };
        }
    }
}
