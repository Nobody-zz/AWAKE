using PersonaWorkbench.Core;

namespace PersonaWorkbench.Web;

/// <summary>请求：指定一个装着 <c>*.persona.json</c> 的目录，做一次全库体检。留空则用工作台自带的卡目录。</summary>
public sealed class CorpusAuditRequest
{
    public string RootPath { get; set; } = string.Empty;
}

public sealed class CorpusAuditResponse
{
    public bool IsSuccess { get; init; }
    public string ErrorCode { get; init; } = string.Empty;
    public string ErrorMessage { get; init; } = string.Empty;
    /// <summary>本次实际体检的目录。留空请求时由服务端自动定位，回传以免使用者不知道查的是哪儿。</summary>
    public string ResolvedRootPath { get; init; } = string.Empty;
    public int CardCount { get; init; }
    public int ErrorCount { get; init; }
    public int WarningCount { get; init; }
    public IReadOnlyList<PersonaCorpusAuditFinding> Findings { get; init; } = Array.Empty<PersonaCorpusAuditFinding>();
    /// <summary>读不进来的文件名（格式/字段不合规）——单独列出，避免它们悄悄从统计里消失。</summary>
    public IReadOnlyList<string> UnreadableFiles { get; init; } = Array.Empty<string>();
}

/// <summary>
/// 把一个目录下的卡全部读进来，交给 <see cref="PersonaCorpusAudit"/> 做跨卡与卡内检查。
///
/// 与存盘路径上的 <see cref="WorkspaceDocumentService"/> 不同：那条只管**一张**卡的**结构**合法性，
/// 这里管**一批**卡的**内容一致性**。
/// </summary>
public sealed class CorpusAuditService
{
    private static readonly PersonaTagRegistry TagRegistry = PersonaTagRegistry.CreateDefault();

    public CorpusAuditResponse Analyze(CorpusAuditRequest? request)
    {
        string requested = request?.RootPath ?? string.Empty;
        if (string.IsNullOrWhiteSpace(requested))
        {
            requested = FindDefaultRoot() ?? string.Empty;
            if (requested.Length == 0)
            {
                return Failure("audit.root_required", "A directory containing .persona.json files is required.");
            }
        }

        string root;
        try
        {
            root = Path.GetFullPath(requested);
        }
        catch (ArgumentException)
        {
            return Failure("audit.root_invalid", "The audit directory path is invalid.");
        }

        if (!Directory.Exists(root))
        {
            return Failure("audit.root_not_found", "The audit directory was not found.");
        }

        List<PersonaDocument> cards = new List<PersonaDocument>();
        List<string> unreadable = new List<string>();
        string[] files;
        try
        {
            files = Directory.GetFiles(root, "*.persona.json", SearchOption.TopDirectoryOnly);
        }
        catch (IOException)
        {
            return Failure("audit.io_error", "The audit directory could not be listed.");
        }
        catch (UnauthorizedAccessException)
        {
            return Failure("audit.access_denied", "The audit directory could not be read.");
        }

        Array.Sort(files, StringComparer.Ordinal);
        foreach (string file in files)
        {
            try
            {
                cards.Add(PersonaDocumentStore.Read(file, TagRegistry));
            }
            catch (PersonaDocumentFormatException)
            {
                unreadable.Add(Path.GetFileName(file));
            }
            catch (IOException)
            {
                unreadable.Add(Path.GetFileName(file));
            }
        }

        PersonaCorpusAuditReport report = PersonaCorpusAudit.Analyze(cards);
        return new CorpusAuditResponse
        {
            IsSuccess = true,
            ResolvedRootPath = root,
            CardCount = report.CardCount,
            ErrorCount = report.ErrorCount,
            WarningCount = report.WarningCount,
            Findings = report.Findings,
            UnreadableFiles = unreadable.AsReadOnly()
        };
    }

    /// <summary>
    /// 从当前工作目录向上最多找 5 层，取第一个"装着 .persona.json 的 characters 目录"。
    /// 用 <c>dotnet run --project src/PersonaWorkbench.Web</c> 启动时工作目录就是该工程目录，
    /// 向上三层即 <c>tools/persona-workbench/characters</c>；把工作台拷到别处也照样成立。
    /// 找不到就返回 null，由调用方退回"必须手填目录"。
    /// </summary>
    public static string? FindDefaultRoot()
    {
        DirectoryInfo? current;
        try
        {
            current = new DirectoryInfo(Directory.GetCurrentDirectory());
        }
        catch (ArgumentException)
        {
            return null;
        }

        for (int depth = 0; depth < 5 && current is not null; depth++)
        {
            string candidate = Path.Combine(current.FullName, "characters");
            if (ContainsCards(candidate)) return candidate;
            current = current.Parent;
        }

        return null;
    }

    private static bool ContainsCards(string directory)
    {
        try
        {
            return Directory.Exists(directory)
                && Directory.EnumerateFiles(directory, "*.persona.json", SearchOption.TopDirectoryOnly).Any();
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static CorpusAuditResponse Failure(string code, string message)
    {
        return new CorpusAuditResponse { IsSuccess = false, ErrorCode = code, ErrorMessage = message };
    }
}
