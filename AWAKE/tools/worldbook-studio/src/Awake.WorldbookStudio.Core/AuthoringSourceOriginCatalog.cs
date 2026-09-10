using System.Text;
using System.Security.Cryptography;

namespace Awake.WorldbookStudio.Core;

internal sealed record AuthoringSourceOrigin(
    string Id,
    string Locator,
    int StartUtf16,
    int EndUtf16,
    string Quote,
    string QuoteHash);

internal static class AuthoringSourceOriginCatalog
{
    /// <summary>
    /// 服务端 locator 口径：来源文件标识（与落地证据、来源登记 locator_root 一致），
    /// 精确定位交给 start_utf16 / end_utf16 与 quote_hash，不再使用合成占位串。
    /// </summary>
    public static IReadOnlyList<AuthoringSourceOrigin> Build(string sourceText, string? sourceName = null)
    {
        var normalized = AuthoringDraftRequestFactory.NormalizeSourceText(sourceText);
        if (normalized.Length == 0) return [];
        // 生产链路（AuthoringDraftRequestFactory）总会带上来源文件标识；不带名称的调用只出现在
        // 旧工具与回归样例里，沿用历史合成 locator，避免凭空产生第三种口径。
        var namedLocator = string.IsNullOrWhiteSpace(sourceName) ? null : sourceName.Trim();

        var origins = new List<AuthoringSourceOrigin>();
        var segmentStart = 0;
        for (var index = 0; index < normalized.Length; index++)
        {
            if (!IsBoundary(normalized[index])) continue;
            AddSegment(normalized, segmentStart, index + 1, origins);
            segmentStart = index + 1;
        }
        AddSegment(normalized, segmentStart, normalized.Length, origins);

        if (origins.Count == 0)
            AddSegment(normalized, 0, normalized.Length, origins);

        return origins
            .Select((origin, index) => origin with
            {
                Id = $"origin-{index + 1:0000}",
                Locator = namedLocator ?? LegacyLocatorForId($"origin-{index + 1:0000}")!
            })
            .ToArray();
    }

    /// <summary>
    /// G8 兼容读取：旧 Worker 与本仓既有回归样例仍可能沿用合成 locator（source unit NNNN）。
    /// 新链路一律以来源文件名为准；该别名只用于判等容忍，不会写回结果。
    /// </summary>
    internal static string? LegacyLocatorForId(string? originId)
        => originId is not null && originId.StartsWith("origin-", StringComparison.Ordinal)
            ? $"source unit {originId["origin-".Length..]}"
            : null;

    private static void AddSegment(
        string sourceText,
        int start,
        int end,
        ICollection<AuthoringSourceOrigin> origins)
    {
        while (start < end && char.IsWhiteSpace(sourceText[start])) start++;
        while (end > start && char.IsWhiteSpace(sourceText[end - 1])) end--;
        if (end <= start) return;

        var quote = sourceText[start..end];
        origins.Add(new AuthoringSourceOrigin(
            string.Empty,
            string.Empty,
            start,
            end,
            quote,
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(quote))).ToLowerInvariant()));
    }

    private static bool IsBoundary(char value)
        => value is '\n' or '\r' or '。' or '！' or '？' or '!' or '?' or '；' or ';';
}
