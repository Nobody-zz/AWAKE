using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;

namespace Awake;

internal sealed class WorldbookVerifiedPackage
{
    internal string ManifestPath { get; set; } = string.Empty;
    internal string RuntimePath { get; set; } = string.Empty;
    internal string IndexPath { get; set; } = string.Empty;
    internal JObject Manifest { get; set; }
    internal JObject Runtime { get; set; }
    internal JObject Index { get; set; }
    internal string ManifestHash { get; set; } = string.Empty;
    internal string ContentHash { get; set; } = string.Empty;
    internal string PackageHash { get; set; } = string.Empty;
}

internal static class WorldbookPackageIntegrity
{
    internal static WorldbookVerifiedPackage ReadAndVerify(
        string manifestPath,
        string expectedPackageId = null,
        string expectedVersion = null,
        string expectedKind = null,
        string expectedManifestHash = null,
        string expectedContentHash = null,
        string expectedPackageHash = null)
    {
        if (string.IsNullOrWhiteSpace(manifestPath)) throw new InvalidOperationException("WB2-MANIFEST-MISSING");
        string fullManifestPath = Path.GetFullPath(manifestPath);
        JObject manifest = ParseJson(fullManifestPath, File.ReadAllBytes(fullManifestPath), "manifest");
        Require(StringEquals(Str(manifest, "schemaVersion"), "awake.worldbook.v2"), "WB2-SCHEMA-UNSUPPORTED:manifest");
        string packageId = Str(manifest, "packageId");
        string version = Str(manifest, "version");
        string kind = Str(manifest, "kind");
        string worldId = Str(manifest, "worldId");
        Require(!string.IsNullOrWhiteSpace(packageId) && !string.IsNullOrWhiteSpace(version) && !string.IsNullOrWhiteSpace(kind) && !string.IsNullOrWhiteSpace(worldId), "WB2-SCHEMA-INVALID:manifest_identity");
        CheckExpected("package_id", packageId, expectedPackageId, "WB2-REFERENCE-MISSING:package_id");
        CheckExpected("version", version, expectedVersion, "WB2-REFERENCE-MISSING:version");
        CheckExpected("kind", kind, expectedKind, "WB2-REFERENCE-MISSING:kind");

        JObject entrypoints = manifest["entrypoints"] as JObject;
        string runtimeRelative = Str(entrypoints, "runtime");
        string indexRelative = Str(entrypoints, "index");
        string root = Path.GetDirectoryName(fullManifestPath) ?? ".";
        string runtimePath = RequireChildPath(root, runtimeRelative, "WB2-MANIFEST-MISSING:runtime");
        string indexPath = RequireChildPath(root, indexRelative, "WB2-MANIFEST-MISSING:index");
        JObject runtime = ParseJson(runtimePath, File.ReadAllBytes(runtimePath), "runtime");
        JObject index = ParseJson(indexPath, File.ReadAllBytes(indexPath), "index");
        Require(StringEquals(Str(runtime, "schemaVersion"), "awake.worldbook.v2"), "WB2-SCHEMA-UNSUPPORTED:runtime");
        Require(StringEquals(Str(index, "schemaVersion"), "awake.worldbook.index.v1"), "WB2-SCHEMA-UNSUPPORTED:index");
        Require(StringEquals(Str(runtime, "packageId"), packageId), "WB2-REFERENCE-MISSING:runtime_package_id");
        Require(StringEquals(Str(runtime, "version"), version), "WB2-REFERENCE-MISSING:runtime_version");
        Require(StringEquals(Str(runtime, "worldId"), worldId), "WB2-REFERENCE-MISSING:runtime_world_id");
        ValidateIndex(runtime, index);

        JObject hashes = manifest["hashes"] as JObject;
        string declaredManifestHash = RequireHash(hashes, "manifestHash");
        string declaredContentHash = RequireHash(hashes, "contentHash");
        string declaredPackageHash = RequireHash(hashes, "packageHash");
        string manifestHash = ComputeManifestHash(manifest);
        string contentHash = ComputeContentHash(runtimeRelative, runtime, indexRelative, index);
        string packageHash = ComputePackageHash(manifestHash, contentHash);
        Require(StringEquals(declaredManifestHash, manifestHash), "WB2-HASH-MISMATCH:manifest");
        Require(StringEquals(declaredContentHash, contentHash), "WB2-HASH-MISMATCH:content");
        Require(StringEquals(declaredPackageHash, packageHash), "WB2-HASH-MISMATCH:package");
        CheckExpected("manifest_hash", manifestHash, expectedManifestHash, "WB2-HASH-MISMATCH:manifest");
        CheckExpected("content_hash", contentHash, expectedContentHash, "WB2-HASH-MISMATCH:content");
        CheckExpected("package_hash", packageHash, expectedPackageHash, "WB2-HASH-MISMATCH:package");

        return new WorldbookVerifiedPackage
        {
            ManifestPath = fullManifestPath,
            RuntimePath = runtimePath,
            IndexPath = indexPath,
            Manifest = manifest,
            Runtime = runtime,
            Index = index,
            ManifestHash = manifestHash,
            ContentHash = contentHash,
            PackageHash = packageHash
        };
    }

    internal static string ComputeManifestHash(JObject manifest)
    {
        JObject input = (JObject)manifest.DeepClone();
        input.Remove("hashes");
        return HashCanonical(input);
    }

    internal static string ComputeContentHash(string runtimePath, JObject runtime, string indexPath, JObject index)
    {
        var files = new[] { (Path: runtimePath, Content: (JToken)runtime), (Path: indexPath, Content: (JToken)index) }
            .OrderBy(x => NormalizePath(x.Path), StringComparer.OrdinalIgnoreCase)
            .ToList();
        var content = new JObject();
        string previousPath = null;
        foreach (var file in files)
        {
            string path = NormalizePath(file.Path);
            if (StringComparer.OrdinalIgnoreCase.Equals(previousPath, path)) throw new InvalidOperationException("WB2-PATH-DUPLICATE:" + path);
            content.Add(new JProperty(path, Canonicalize(file.Content)));
            previousPath = path;
        }
        return HashCanonical(content);
    }

    internal static string ComputePackageHash(string manifestHash, string contentHash)
    {
        byte[] manifestBytes = ParseHashBytes(manifestHash);
        byte[] contentBytes = ParseHashBytes(contentHash);
        using (SHA256 sha = SHA256.Create()) return ToHex(sha.ComputeHash(manifestBytes.Concat(contentBytes).ToArray()));
    }

    internal static string NormalizePath(string path) => (path ?? string.Empty).Replace('\\', '/').TrimStart('/');

    private static void ValidateIndex(JObject runtime, JObject index)
    {
        JArray runtimeEntries = runtime["entries"] as JArray;
        JArray indexEntries = index["entryIds"] as JArray;
        Require(runtimeEntries != null && indexEntries != null, "WB2-INDEX-MISMATCH:entry_ids_missing");
        JArray expectedEntries = new JArray(runtimeEntries.Children<JObject>().Select(x => x["id"]?.Value<string>() ?? string.Empty));
        Require(Canonicalize(expectedEntries).ToString(Newtonsoft.Json.Formatting.None) == Canonicalize(indexEntries).ToString(Newtonsoft.Json.Formatting.None), "WB2-INDEX-MISMATCH:entry_ids");
        JObject runtimeIndexes = runtime["indexes"] as JObject;
        Require(runtimeIndexes != null && index["keywordToEntryIds"] is JObject && index["domainToEntryIds"] is JObject, "WB2-INDEX-MISMATCH:indexes_missing");
        Require(Canonicalize(runtimeIndexes["keywordToEntryIds"] ?? new JObject()).ToString(Newtonsoft.Json.Formatting.None) == Canonicalize(index["keywordToEntryIds"]).ToString(Newtonsoft.Json.Formatting.None), "WB2-INDEX-MISMATCH:keywords");
        Require(Canonicalize(runtimeIndexes["domainToEntryIds"] ?? new JObject()).ToString(Newtonsoft.Json.Formatting.None) == Canonicalize(index["domainToEntryIds"]).ToString(Newtonsoft.Json.Formatting.None), "WB2-INDEX-MISMATCH:domains");
    }

    private static JObject ParseJson(string path, byte[] bytes, string label)
    {
        try { return JObject.Parse(new UTF8Encoding(false, true).GetString(bytes)); }
        catch (Exception ex) { throw new InvalidOperationException("WB2-JSON-INVALID:" + label, ex); }
    }

    private static string RequireChildPath(string root, string relative, string missingCode)
    {
        if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative)) throw new InvalidOperationException("WB2-PATH-ESCAPE");
        string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string full = Path.GetFullPath(Path.Combine(root, relative));
        if (!full.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("WB2-PATH-ESCAPE");
        if (!File.Exists(full)) throw new FileNotFoundException(missingCode, full);
        return full;
    }

    private static string RequireHash(JObject hashes, string name)
    {
        string value = Str(hashes, name);
        if (value.Length != 64 || !value.All(IsHex)) throw new InvalidOperationException("WB2-HASH-MISSING:" + name);
        return value.ToUpperInvariant();
    }

    private static byte[] ParseHashBytes(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length != 64 || !value.All(IsHex)) throw new InvalidOperationException("WB2-HASH-INVALID");
        byte[] bytes = new byte[32];
        for (int i = 0; i < bytes.Length; i++) bytes[i] = Convert.ToByte(value.Substring(i * 2, 2), 16);
        return bytes;
    }

    private static string HashCanonical(JToken token)
    {
        string json = SerializeCanonical(token);
        using (SHA256 sha = SHA256.Create()) return ToHex(sha.ComputeHash(Encoding.UTF8.GetBytes(json)));
    }

    private static string SerializeCanonical(JToken token)
    {
        var builder = new StringBuilder();
        WriteCanonical(builder, token);
        return builder.ToString();
    }

    private static void WriteCanonical(StringBuilder builder, JToken token)
    {
        if (token is JObject obj)
        {
            builder.Append('{');
            bool first = true;
            foreach (JProperty property in obj.Properties().OrderBy(x => x.Name, StringComparer.Ordinal))
            {
                if (!first) builder.Append(',');
                first = false;
                WriteString(builder, property.Name);
                builder.Append(':');
                WriteCanonical(builder, property.Value);
            }
            builder.Append('}');
            return;
        }
        if (token is JArray array)
        {
            builder.Append('[');
            bool first = true;
            foreach (JToken child in array)
            {
                if (!first) builder.Append(',');
                first = false;
                WriteCanonical(builder, child);
            }
            builder.Append(']');
            return;
        }
        if (token.Type == JTokenType.Null || token.Type == JTokenType.Undefined)
        {
            builder.Append("null");
            return;
        }
        if (token is JValue value && value.Type == JTokenType.String)
        {
            WriteString(builder, value.Value<string>() ?? string.Empty);
            return;
        }
        if (token is JValue primitive)
        {
            builder.Append(primitive.ToString(Newtonsoft.Json.Formatting.None));
            return;
        }
        throw new InvalidOperationException("WB2-JSON-UNSUPPORTED:" + token.Type);
    }

    private static void WriteString(StringBuilder builder, string value)
    {
        builder.Append('"');
        foreach (char character in value ?? string.Empty)
        {
            switch (character)
            {
                case '"': builder.Append("\\\""); break;
                case '\\': builder.Append("\\\\"); break;
                case '\b': builder.Append("\\b"); break;
                case '\f': builder.Append("\\f"); break;
                case '\n': builder.Append("\\n"); break;
                case '\r': builder.Append("\\r"); break;
                case '\t': builder.Append("\\t"); break;
                default:
                    if (character < 0x20 || character > 0x7E || character == '<' || character == '>' || character == '&' || character == '\'' || character == '+' || character == '`')
                    {
                        builder.Append("\\u");
                        builder.Append(((int)character).ToString("X4", System.Globalization.CultureInfo.InvariantCulture));
                    }
                    else builder.Append(character);
                    break;
            }
        }
        builder.Append('"');
    }

    private static JToken Canonicalize(JToken token)
    {
        if (token is JObject obj)
        {
            var result = new JObject();
            foreach (JProperty property in obj.Properties().OrderBy(x => x.Name, StringComparer.Ordinal))
                result.Add(new JProperty(property.Name, Canonicalize(property.Value)));
            return result;
        }
        if (token is JArray array)
        {
            var result = new JArray();
            foreach (JToken child in array) result.Add(Canonicalize(child));
            return result;
        }
        return token.DeepClone();
    }

    private static void CheckExpected(string label, string actual, string expected, string code)
    {
        if (!string.IsNullOrWhiteSpace(expected) && !StringEquals(actual, expected)) throw new InvalidOperationException(code);
    }

    private static void Require(bool condition, string code)
    {
        if (!condition) throw new InvalidOperationException(code);
    }

    private static bool StringEquals(string left, string right) => StringComparer.OrdinalIgnoreCase.Equals(left ?? string.Empty, right ?? string.Empty);
    private static bool IsHex(char value) => (value >= '0' && value <= '9') || (value >= 'a' && value <= 'f') || (value >= 'A' && value <= 'F');
    private static string Str(JObject value, string name) => value?[name]?.Value<string>() ?? string.Empty;
    private static string ToHex(byte[] bytes) => BitConverter.ToString(bytes).Replace("-", string.Empty).ToUpperInvariant();
}
