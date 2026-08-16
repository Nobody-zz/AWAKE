using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace Awake;

internal sealed class AwakeRuleManifest
{
    internal string SchemaVersion { get; set; } = "awake.rule.v1";
    internal string Id { get; set; } = string.Empty;
    internal string Group { get; set; } = "core";
    internal int Priority { get; set; }
    internal bool Enabled { get; set; } = true;
    internal string Fingerprint { get; set; } = string.Empty;
    internal JObject Payload { get; set; } = new JObject();
    internal JObject Raw { get; set; }
}

internal static class AwakeRuleRegistry
{
    internal const string SupportedSchemaVersion = "awake.rule.v1";
    private static readonly object Gate = new object();
    private static readonly Dictionary<string, AwakeRuleManifest> Rules =
        new Dictionary<string, AwakeRuleManifest>(StringComparer.Ordinal);
    private static readonly Regex ValidIdRegex = new Regex(
        "^[a-z0-9][a-z0-9_.-]{0,127}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static bool _loaded;

    internal static bool Register(AwakeRuleManifest manifest)
    {
        string error;
        if (!Validate(manifest, out error))
        {
            AwakeLog.Write("awake_rule_register_invalid id=" + (manifest?.Id ?? "null") + " error=" + error);
            return false;
        }
        lock (Gate)
        {
            Rules[manifest.Id] = manifest;
            return true;
        }
    }

    internal static bool TryGet(string id, out AwakeRuleManifest manifest)
    {
        lock (Gate)
        {
            return Rules.TryGetValue(id ?? string.Empty, out manifest);
        }
    }

    internal static IReadOnlyList<AwakeRuleManifest> All()
    {
        lock (Gate)
        {
            List<AwakeRuleManifest> list = new List<AwakeRuleManifest>(Rules.Values);
            list.Sort((a, b) =>
            {
                int priority = a.Priority.CompareTo(b.Priority);
                if (priority != 0) return priority;
                int group = string.CompareOrdinal(a.Group, b.Group);
                if (group != 0) return group;
                return string.CompareOrdinal(a.Id, b.Id);
            });
            return list;
        }
    }

    internal static bool Validate(AwakeRuleManifest manifest, out string error)
    {
        error = null;
        if (manifest == null)
        {
            error = "null_manifest";
            return false;
        }
        if (!StringComparer.Ordinal.Equals(manifest.SchemaVersion, SupportedSchemaVersion))
        {
            error = "unsupported_schema:" + (manifest.SchemaVersion ?? "null");
            return false;
        }
        if (string.IsNullOrWhiteSpace(manifest.Id) || !ValidIdRegex.IsMatch(manifest.Id))
        {
            error = "invalid_id";
            return false;
        }
        if (manifest.Priority < 0 || manifest.Priority > 1000)
        {
            error = "priority_out_of_range";
            return false;
        }
        if (manifest.Fingerprint != null && manifest.Fingerprint.Length > 128)
        {
            error = "fingerprint_too_long";
            return false;
        }
        return true;
    }

    internal static void EnsureLoaded()
    {
        lock (Gate)
        {
            if (_loaded) return;
            _loaded = true;
        }
        string directory = LocateDirectory();
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory)) return;
        try
        {
            foreach (string file in Directory.GetFiles(directory, "*.json", SearchOption.TopDirectoryOnly))
            {
                LoadFile(file);
            }
        }
        catch (Exception ex)
        {
            AwakeLog.Write("awake_rule_registry_load_error error=" + ex.Message);
        }
    }

    internal static void ResetForTesting()
    {
        lock (Gate)
        {
            Rules.Clear();
            _loaded = false;
        }
    }

    private static void LoadFile(string path)
    {
        try
        {
            JObject root = JObject.Parse(File.ReadAllText(path));
            AwakeRuleManifest manifest = ParseManifest(root);
            if (Register(manifest))
            {
                AwakeLog.Write("awake_rule_registered id=" + manifest.Id + " group=" + manifest.Group);
            }
            else
            {
                AwakeLog.Write("awake_rule_rejected file=" + Path.GetFileName(path));
            }
        }
        catch (Exception ex)
        {
            AwakeLog.Write("awake_rule_file_error file=" + Path.GetFileName(path) + " error=" + ex.Message);
        }
    }

    private static AwakeRuleManifest ParseManifest(JObject root)
    {
        AwakeRuleManifest manifest = new AwakeRuleManifest
        {
            SchemaVersion = StringValue(root["schemaVersion"]) ?? SupportedSchemaVersion,
            Id = StringValue(root["id"]) ?? string.Empty,
            Group = StringValue(root["group"]) ?? "core",
            Priority = IntValue(root["priority"]),
            Enabled = BoolValue(root["enabled"]) ?? true,
            Fingerprint = StringValue(root["fingerprint"]) ?? string.Empty,
            Payload = root["payload"] as JObject ?? new JObject(),
            Raw = (JObject)root.DeepClone()
        };
        return manifest;
    }

    private static string LocateDirectory()
    {
        string assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? ".";
        DirectoryInfo current = new DirectoryInfo(assemblyDir);
        for (int i = 0; i < 6 && current != null; i++)
        {
            string candidate = Path.Combine(current.FullName, "ModuleData", "Rules");
            if (Directory.Exists(candidate)) return candidate;
            candidate = Path.Combine(current.FullName, "Rules");
            if (Directory.Exists(candidate)) return candidate;
            current = current.Parent;
        }
        return null;
    }

    private static string StringValue(JToken token)
    {
        return token == null ? null : token.ToString();
    }

    private static int IntValue(JToken token)
    {
        if (token == null || token.Type != JTokenType.Integer) return 0;
        try { return (int)token; } catch { return 0; }
    }

    private static bool? BoolValue(JToken token)
    {
        if (token == null || token.Type != JTokenType.Boolean) return null;
        return (bool)token;
    }
}
