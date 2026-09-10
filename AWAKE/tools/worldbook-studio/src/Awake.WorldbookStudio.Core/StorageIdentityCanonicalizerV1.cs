using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Security.Cryptography;

namespace Awake.WorldbookStudio.Core;

internal enum StorageRecordFamily
{
    CompileProof,
    Operation,
    CompileResult,
    CommitMarker,
    CompileReservation
}

internal static class StorageIdentityCanonicalizerV1
{
    private static readonly IReadOnlyDictionary<StorageRecordFamily, string> Codes = new Dictionary<StorageRecordFamily, string>
    {
        [StorageRecordFamily.CompileProof] = "cp",
        [StorageRecordFamily.Operation] = "op",
        [StorageRecordFamily.CompileResult] = "res",
        [StorageRecordFamily.CommitMarker] = "mk",
        [StorageRecordFamily.CompileReservation] = "rv"
    };

    private static readonly IReadOnlyDictionary<StorageRecordFamily, string> Names = new Dictionary<StorageRecordFamily, string>
    {
        [StorageRecordFamily.CompileProof] = "compile_proof",
        [StorageRecordFamily.Operation] = "operation",
        [StorageRecordFamily.CompileResult] = "compile_result",
        [StorageRecordFamily.CommitMarker] = "commit_marker",
        [StorageRecordFamily.CompileReservation] = "compile_reservation"
    };

    public static string Key(StorageRecordFamily family, IReadOnlyDictionary<string, object?> identity)
    {
        var bytes = CanonicalBytes(family, identity);
        return $"k1_{Codes[family]}_{Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()}";
    }

    public static byte[] CanonicalBytes(StorageRecordFamily family, IReadOnlyDictionary<string, object?> identity)
    {
        if (identity is null) throw new ArgumentNullException(nameof(identity));
        var identityNode = new JsonObject();
        foreach (var pair in identity.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(pair.Key)) throw new InvalidOperationException("WB-AUTHORITY-SAFEID-422: identity key 不能为空。");
            identityNode[pair.Key] = NormalizeValue(pair.Value);
        }

        var root = new JsonObject
        {
            ["kind"] = Names[family],
            ["identity"] = identityNode
        };
        var json = root.ToJsonString(new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.Default,
            WriteIndented = false
        });
        return new UTF8Encoding(false).GetBytes(json);
    }

    public static string OldSafeIdV0(string value)
    {
        if (value is null) throw new ArgumentNullException(nameof(value));
        return value.Replace("/", "_", StringComparison.Ordinal)
            .Replace("\\", "_", StringComparison.Ordinal)
            .Replace("..", "_", StringComparison.Ordinal);
    }

    private static JsonNode NormalizeValue(object? value)
    {
        return value switch
        {
            string text => JsonValue.Create(NormalizeText(text))!,
            int number => JsonValue.Create(number)!,
            long number => JsonValue.Create(number)!,
            null => throw new InvalidOperationException("WB-AUTHORITY-SAFEID-422: identity 值不能为空。"),
            _ => throw new InvalidOperationException("WB-AUTHORITY-SAFEID-422: identity 只允许字符串或整数。")
        };
    }

    private static string NormalizeText(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormC);
        if (normalized.Any(char.IsControl)) throw new InvalidOperationException("WB-AUTHORITY-SAFEID-422: identity 不得包含控制字符。");
        return normalized;
    }
}
