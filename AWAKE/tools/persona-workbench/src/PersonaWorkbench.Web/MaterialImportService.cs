using System.Text;

namespace PersonaWorkbench.Web;

public sealed class MaterialSourceRequest
{
    public string SourceFile { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
}

public sealed class MaterialSegmentRequest
{
    public List<MaterialSourceRequest> Sources { get; set; } = new List<MaterialSourceRequest>();
}

public sealed class MaterialSegment
{
    public string ItemId { get; init; } = string.Empty;
    public int SourceOrdinal { get; init; }
    public int SegmentOrdinal { get; init; }
    public string SourceFile { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Text { get; init; } = string.Empty;
}

public sealed class MaterialSegmentResponse
{
    public bool IsValid { get; init; }
    public int InputBytes { get; init; }
    public List<string> Errors { get; init; } = new List<string>();
    public List<MaterialSegment> Segments { get; init; } = new List<MaterialSegment>();
}

public static class MaterialImportService
{
    private const int MaximumSources = 32;
    private const int MaximumSourceBytes = 256 * 1024;
    private const int MaximumTotalBytes = 512 * 1024;
    private const int MaximumSegmentBytes = 12 * 1024;
    private const int MaximumSegments = 64;

    public static MaterialSegmentResponse Segment(MaterialSegmentRequest? request)
    {
        List<string> errors = new List<string>();
        List<MaterialSegment> segments = new List<MaterialSegment>();
        if (request?.Sources == null || request.Sources.Count == 0)
        {
            errors.Add("请先粘贴资料，或选择至少一个 .txt/.md 文件。");
            return new MaterialSegmentResponse { IsValid = false, Errors = errors, Segments = segments };
        }

        if (request.Sources.Count > MaximumSources)
        {
            errors.Add($"一次最多读取 {MaximumSources} 个资料来源；请分批导入。");
            return new MaterialSegmentResponse { IsValid = false, Errors = errors, Segments = segments };
        }

        int inputBytes = 0;
        for (int sourceIndex = 0; sourceIndex < request.Sources.Count; sourceIndex++)
        {
            MaterialSourceRequest source = request.Sources[sourceIndex] ?? new MaterialSourceRequest();
            string text = NormalizeText(source.Text);
            string sourceFile = NormalizeSourceFile(source.SourceFile, sourceIndex + 1);
            int sourceBytes = Encoding.UTF8.GetByteCount(text);
            inputBytes += sourceBytes;
            if (sourceBytes == 0)
            {
                errors.Add($"资料来源“{sourceFile}”没有可读取的文字。");
                continue;
            }
            if (sourceBytes > MaximumSourceBytes)
            {
                errors.Add($"资料来源“{sourceFile}”超过 256 KiB；请拆成较小文件。");
                continue;
            }
            if (inputBytes > MaximumTotalBytes)
            {
                errors.Add("本次资料总量超过 512 KiB；请分批导入。");
                break;
            }

            string? pendingHeading = null;
            StringBuilder paragraph = new StringBuilder();
            foreach (string line in text.Split('\n'))
            {
                string trimmed = line.Trim();
                if (IsHeading(trimmed))
                {
                    AddParagraphSegments(segments, paragraph.ToString(), pendingHeading, sourceFile, sourceIndex + 1);
                    paragraph.Clear();
                    pendingHeading = NormalizeHeading(trimmed);
                    continue;
                }
                if (trimmed.Length == 0)
                {
                    AddParagraphSegments(segments, paragraph.ToString(), pendingHeading, sourceFile, sourceIndex + 1);
                    paragraph.Clear();
                    continue;
                }
                if (paragraph.Length > 0) paragraph.Append('\n');
                paragraph.Append(line.TrimEnd());
            }
            AddParagraphSegments(segments, paragraph.ToString(), pendingHeading, sourceFile, sourceIndex + 1);
            if (segments.Count >= MaximumSegments) break;
        }

        if (segments.Count == 0 && errors.Count == 0) errors.Add("没有识别出可编辑的资料段落。");
        if (segments.Count > MaximumSegments) segments = segments.Take(MaximumSegments).ToList();
        List<MaterialSegment> numbered = segments.Select((segment, index) => new MaterialSegment
        {
            ItemId = "segment-" + (index + 1).ToString("D3"),
            SourceOrdinal = index + 1,
            SegmentOrdinal = index + 1,
            SourceFile = segment.SourceFile,
            Title = segment.Title,
            Text = segment.Text
        }).ToList();
        return new MaterialSegmentResponse
        {
            IsValid = errors.Count == 0 && numbered.Count > 0,
            InputBytes = inputBytes,
            Errors = errors,
            Segments = numbered
        };
    }

    private static void AddParagraphSegments(List<MaterialSegment> segments, string rawText, string? heading, string sourceFile, int sourceOrdinal)
    {
        string text = rawText.Trim();
        if (text.Length == 0 || segments.Count >= MaximumSegments) return;
        List<string> chunks = SplitLongText(text);
        for (int index = 0; index < chunks.Count && segments.Count < MaximumSegments; index++)
        {
            string chunk = chunks[index];
            string title = string.IsNullOrWhiteSpace(heading) ? DeriveTitle(chunk, segments.Count + 1) : heading.Trim();
            if (chunks.Count > 1) title += " · 片段 " + (index + 1);
            segments.Add(new MaterialSegment
            {
                SourceOrdinal = sourceOrdinal,
                SourceFile = sourceFile,
                Title = LimitText(title, 120),
                Text = chunk
            });
        }
    }

    private static List<string> SplitLongText(string text)
    {
        List<string> chunks = new List<string>();
        int start = 0;
        while (start < text.Length)
        {
            int end = start;
            while (end < text.Length && Encoding.UTF8.GetByteCount(text[start..(end + 1)]) <= MaximumSegmentBytes) end++;
            if (end == text.Length)
            {
                chunks.Add(text[start..].Trim());
                break;
            }
            int breakAt = end;
            for (int index = end; index > start + 120; index--)
            {
                if (IsSentenceBoundary(text[index - 1]))
                {
                    breakAt = index;
                    break;
                }
            }
            if (breakAt <= start) breakAt = end;
            if (breakAt > start && char.IsHighSurrogate(text[breakAt - 1])) breakAt--;
            string chunk = text[start..breakAt].Trim();
            if (chunk.Length > 0) chunks.Add(chunk);
            start = breakAt;
            while (start < text.Length && char.IsWhiteSpace(text[start])) start++;
        }
        return chunks;
    }

    private static bool IsHeading(string value)
    {
        if (value.Length == 0 || value.Length > 120) return false;
        if (value.StartsWith('#')) return true;
        return (value.EndsWith(':') || value.EndsWith('：'))
            && !value.Contains('。')
            && !value.Contains('！')
            && !value.Contains('？');
    }

    private static string NormalizeHeading(string value)
    {
        string normalized = value.Trim().TrimStart('#').Trim();
        return normalized.TrimEnd(':', '：').Trim();
    }

    private static string DeriveTitle(string text, int ordinal)
    {
        string firstLine = text.Split('\n', 2)[0].Trim();
        string candidate = firstLine;
        int boundary = candidate.IndexOfAny(new[] { '。', '！', '？', '.', '!', '?' });
        if (boundary > 0) candidate = candidate[..boundary];
        if (string.IsNullOrWhiteSpace(candidate)) candidate = "资料片段 " + ordinal;
        return LimitText(candidate, 80);
    }

    private static bool IsSentenceBoundary(char value) => value is '。' or '！' or '？' or '.' or '!' or '?';

    private static string NormalizeText(string value)
    {
        return (value ?? string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Normalize(NormalizationForm.FormC).Trim();
    }

    private static string NormalizeSourceFile(string value, int ordinal)
    {
        string normalized = (value ?? string.Empty).Trim().Replace('\\', '/');
        int slash = normalized.LastIndexOf('/');
        if (slash >= 0) normalized = normalized[(slash + 1)..];
        return string.IsNullOrWhiteSpace(normalized) ? "粘贴资料 " + ordinal : LimitText(normalized, 160);
    }

    private static string LimitText(string value, int maximumCharacters)
    {
        return value.Length <= maximumCharacters ? value : value[..maximumCharacters].TrimEnd() + "…";
    }
}
