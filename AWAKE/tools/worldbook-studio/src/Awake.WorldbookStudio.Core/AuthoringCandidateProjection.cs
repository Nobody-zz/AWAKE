namespace Awake.WorldbookStudio.Core;

internal sealed record AuthoringCandidateTarget(
    int Step,
    string StepTitle,
    string Label,
    string FocusId,
    int OperationCount);

internal static class AuthoringCandidateProjection
{
    internal static AuthoringCandidateTarget? Project(KnowledgePatch? patch)
    {
        if (patch is null || patch.Operations.Count == 0) return null;
        if (patch.Operations.Count != 1) return null;
        return ProjectPath(patch.Operations[0].Path);
    }

    private static AuthoringCandidateTarget? ProjectPath(string? path)
    {
        var segments = ParsePath(path);
        if (segments is null) return null;
        if (segments.Length == 2 && segments[0] == "title" && IsChineseLocale(segments[1]))
            return new(0, "基本信息", "档案标题", "doc-title", 1);
        if (segments.Length == 2 && segments[0] == "summary" && IsChineseLocale(segments[1]))
            return new(0, "基本信息", "档案摘要", "doc-summary", 1);
        if (segments.Length == 4 && segments[0] == "assertions" && TryIndex(segments[1], out var assertionIndex)
            && segments[2] == "text" && IsChineseLocale(segments[3]))
            return new(1, "客观事实", "客观事实", $"fact-{assertionIndex}-text", 1);
        if (segments.Length == 6 && segments[0] == "assertions" && TryIndex(segments[1], out var factIndex)
            && segments[2] == "expressions" && TryIndex(segments[3], out var expressionIndex)
            && segments[4] == "text" && IsChineseLocale(segments[5]))
            return new(2, "NPC 表达", "NPC 表达", $"expression-{factIndex}-{expressionIndex}-text", 1);
        return null;
    }

    private static string[]? ParsePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !path.StartsWith("/", StringComparison.Ordinal)) return null;
        return path[1..]
            .Split('/')
            .Select(segment => segment.Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal))
            .ToArray();
    }

    private static bool TryIndex(string value, out int index)
        => int.TryParse(value, out index) && index >= 0;

    private static bool IsChineseLocale(string value)
        => value is "zh-CN" or "zh";
}
