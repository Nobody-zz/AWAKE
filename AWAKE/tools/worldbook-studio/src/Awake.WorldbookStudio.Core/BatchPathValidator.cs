using System.Text;

namespace Awake.WorldbookStudio.Core;

internal static class BatchPathValidator
{
    public static string RequireRelativePath(string root, string relativePath, string operation, IReadOnlyCollection<string>? allowedExtensions = null)
    {
        var normalized = ValidateRelativeText(relativePath, operation);
        var segments = normalized.Split('/', StringSplitOptions.None);
        if (segments.Any(x => x is "." or ".." || x.Length == 0))
            throw new InvalidOperationException($"WB-BATCH-PATH-422: {operation} 包含非法路径段。");
        if (segments.Any(x => x.Contains(':', StringComparison.Ordinal)))
            throw new InvalidOperationException($"WB-BATCH-PATH-422: {operation} 不允许备用数据流路径。");
        if (allowedExtensions is not null && !allowedExtensions.Any(x => x.Equals(Path.GetExtension(normalized), StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"WB-BATCH-PATH-422: {operation} 文件扩展名不受支持。");

        var fullRoot = Path.GetFullPath(root);
        var fullPath = Path.GetFullPath(Path.Combine(fullRoot, normalized.Replace('/', Path.DirectorySeparatorChar)));
        if (!WorkspaceRootGuard.IsSameOrInside(fullPath, fullRoot))
            throw new InvalidOperationException($"WB-BATCH-PATH-422: {operation} 超出允许目录。");
        WorkspacePathPolicy.EnsureNoReparsePoint(fullRoot);
        WorkspacePathPolicy.EnsureNoReparsePoint(fullPath);
        return fullPath;
    }

    public static string RequireIdentifier(string value, string operation)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Contains('\u005C') || value.Contains('/', StringComparison.Ordinal) || value.Contains('\0') || value.Contains(':', StringComparison.Ordinal))
            throw new InvalidOperationException($"WB-BATCH-ID-422: {operation} 标识符无效。");
        if (value is "." or "..") throw new InvalidOperationException($"WB-BATCH-ID-422: {operation} 标识符无效。");
        return value.Normalize(NormalizationForm.FormC);
    }

    public static void EnsureNoCollisions(IEnumerable<string> relativePaths, string operation)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in relativePaths)
        {
            var normalized = ValidateRelativeText(path, operation);
            if (!seen.Add(CollisionKey(normalized)))
                throw new InvalidOperationException($"WB-BATCH-PATH-409: {operation} 存在 Windows 大小写或 Unicode 规范化冲突。");
        }
    }

    public static string CollisionKey(string relativePath)
    {
        var normalized = ValidateRelativeText(relativePath, "路径");
        return string.Join('/', normalized.Split('/', StringSplitOptions.None).Select(x => x.Normalize(NormalizationForm.FormC).ToUpperInvariant()));
    }

    private static string ValidateRelativeText(string relativePath, string operation)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || relativePath.Contains('\u005C') || relativePath.Contains('\0'))
            throw new InvalidOperationException($"WB-BATCH-PATH-422: {operation} 路径格式无效。");
        if (Path.IsPathRooted(relativePath) || relativePath.StartsWith("/", StringComparison.Ordinal) || relativePath.StartsWith("//", StringComparison.Ordinal))
            throw new InvalidOperationException($"WB-BATCH-PATH-422: {operation} 不允许绝对路径。");
        if (relativePath.StartsWith("\\Device\\", StringComparison.OrdinalIgnoreCase) || relativePath.StartsWith("\\\\?\\", StringComparison.Ordinal))
            throw new InvalidOperationException($"WB-BATCH-PATH-422: {operation} 不允许设备路径。");
        return relativePath.Normalize(NormalizationForm.FormC);
    }
}
