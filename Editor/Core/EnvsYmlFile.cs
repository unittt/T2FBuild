using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace T2FBuild.Editor
{
    public static class EnvsYmlFile
    {
        public const string DefaultFileName = "envs.yml";

        public class EnvField
        {
            public string Key;

            public string Value;

            public bool IsBlock;
        }

        public static string ResolveDefaultPath()
        {
            var root = Path.GetDirectoryName(Application.dataPath);
            return string.IsNullOrEmpty(root) ? null : Path.Combine(root, DefaultFileName).Replace('\\', '/');
        }

        public static List<EnvField> Read(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return new List<EnvField>();
            try
            {
                return Parse(File.ReadAllText(path));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[T2FBuild] Failed to read {path}: {e.Message}");
                return new List<EnvField>();
            }
        }

        public static bool TryRead(string path, string key, out string value)
        {
            value = null;
            foreach (var f in Read(path))
            {
                if (f.Key == key) { value = f.Value; return true; }
            }
            return false;
        }

        public static void Write(string path, IEnumerable<EnvField> updates)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));
            var original = File.Exists(path) ? File.ReadAllText(path) : string.Empty;
            var dict = updates.Where(u => u != null && !string.IsNullOrEmpty(u.Key)).ToDictionary(u => u.Key, u => u);
            var merged = Merge(original, dict);
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(path, merged);
        }

        public static Dictionary<string, string> InjectIntoProcess(string path)
        {
            var originals = new Dictionary<string, string>();
            foreach (var f in Read(path))
            {
                if (string.IsNullOrEmpty(f.Key)) continue;
                if (IsPlaceholder(f.Value)) continue;
                if (!originals.ContainsKey(f.Key))
                {
                    originals[f.Key] = Environment.GetEnvironmentVariable(f.Key);
                }
                Environment.SetEnvironmentVariable(f.Key, f.Value);
            }
            return originals;
        }

        public static void RestoreProcess(Dictionary<string, string> originals)
        {
            if (originals == null) return;
            foreach (var kv in originals)
            {
                Environment.SetEnvironmentVariable(kv.Key, kv.Value);
            }
        }

        public static bool IsPlaceholder(string value)
        {
            return !string.IsNullOrEmpty(value) && value.StartsWith("REPLACE_WITH_", StringComparison.OrdinalIgnoreCase);
        }

        public static List<EnvField> Parse(string content)
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

        public static string Merge(string original, Dictionary<string, EnvField> updates)
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
    }
}
