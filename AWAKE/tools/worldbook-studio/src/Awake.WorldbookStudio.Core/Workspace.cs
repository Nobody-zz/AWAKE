using System.Text.Json;
using System.Text.Json.Nodes;

namespace Awake.WorldbookStudio.Core;

public static class WorkspaceRootGuard
{
    public static void Validate(string root, string? schemaRoot = null)
    {
        var fullRoot = Path.GetFullPath(root);
        var protectedRoots = new List<string>
        {
            @"D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord",
            @"D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\AWAKE",
            @"D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\AWAKE\ModuleData",
            @"D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\AWAKE\PlayerExports",
            @"D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\AnimusForge\PlayerExports",
        };

        if (!string.IsNullOrWhiteSpace(schemaRoot))
        {
            var awakeRoot = Directory.GetParent(Path.GetFullPath(schemaRoot))?.Parent?.FullName;
            if (!string.IsNullOrWhiteSpace(awakeRoot))
            {
                protectedRoots.Add(Path.Combine(awakeRoot, "ModuleData"));
                protectedRoots.Add(Path.Combine(awakeRoot, "dist"));
                protectedRoots.Add(Path.Combine(awakeRoot, "_build_out"));
                protectedRoots.Add(Path.Combine(awakeRoot, "candidate_frozen"));
                protectedRoots.Add(Path.Combine(awakeRoot, "pending_game"));
            }
        }

        foreach (var protectedRoot in protectedRoots)
        {
            if (IsSameOrInside(fullRoot, protectedRoot))
            {
                throw new InvalidOperationException($"WB-ROOT-001: workspace 根目录不能位于受保护路径或成为其祖先。{fullRoot}");
            }
        }

        if (Path.GetPathRoot(fullRoot)?.Equals(fullRoot, StringComparison.OrdinalIgnoreCase) == true)
        {
            throw new InvalidOperationException($"WB-ROOT-002: workspace 根目录不能是磁盘根目录。{fullRoot}");
        }
    }

    public static bool IsSameOrInside(string path, string root)
    {
        var normalizedPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return normalizedPath.Equals(normalizedRoot, StringComparison.OrdinalIgnoreCase)
            || normalizedPath.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || normalizedPath.StartsWith(normalizedRoot + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class WorkspaceWritePolicy
{
    public string Root { get; }
    private string AuthoringRoot => Path.Combine(Root, "authoring");
    private string FixturesRoot => Path.Combine(Root, "fixtures");
    private string SuggestionsRoot => Path.Combine(Root, "authoring", "suggestions");
    private string CompiledRoot => Path.Combine(Root, "compiled");
    private string ExportRoot => Path.Combine(Root, "export", "WorldbookV2");
    private string PrebatchesRoot => Path.Combine(Root, "prebatches");
    private string BatchesRoot => Path.Combine(Root, "batches");
    private string CacheRoot => Path.Combine(Root, "cache");

    public WorkspaceWritePolicy(string root)
    {
        Root = Path.GetFullPath(root);
        WorkspaceRootGuard.Validate(Root);
        WorkspacePathPolicy.ValidateWorkspaceRoot(Root);
    }

    public string RequireAuthoring(string candidate, string operation = "authoring") => RequireWithin(candidate, AuthoringRoot, operation, true);
    public string RequireSuggestions(string candidate, string operation = "suggestions") => RequireWithin(candidate, SuggestionsRoot, operation, true);
    public string RequireFixtures(string candidate, string operation = "fixtures") => RequireWithin(candidate, FixturesRoot, operation, true);
    public string RequireCompiled(string candidate, string operation = "compiled") => RequireWithin(candidate, CompiledRoot, operation, true);
    public string RequireCompiledTransaction(string candidate, string parentRoot, string prefix, string operation = "compiled transaction")
    {
        var full = Path.GetFullPath(candidate);
        var transactionParent = Path.GetFullPath(parentRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var compiledRoot = Path.GetFullPath(CompiledRoot);
        var actualParent = (Path.GetDirectoryName(full) ?? string.Empty).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var name = Path.GetFileName(full);
        var parentAllowed = transactionParent.Equals(Root, StringComparison.OrdinalIgnoreCase)
            || WorkspaceRootGuard.IsSameOrInside(transactionParent, compiledRoot);
        if (!WorkspaceRootGuard.IsSameOrInside(transactionParent, Root)
            || !parentAllowed
            || !actualParent.Equals(transactionParent, StringComparison.OrdinalIgnoreCase)
            || !name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"WB-PATH-006: {operation} 必须是 compiled 输出目录的同级事务目录。{full}");
        }

        EnsureNoReparsePoint(full);
        return full;
    }
    public string RequireCompiledTransactionFile(string candidate, string transactionRoot, string operation = "compiled transaction file")
    {
        var full = Path.GetFullPath(candidate);
        var transactionRootFull = Path.GetFullPath(transactionRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!WorkspaceRootGuard.IsSameOrInside(transactionRootFull, Root)
            || !WorkspaceRootGuard.IsSameOrInside(full, transactionRootFull)
            || full.Equals(transactionRootFull, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"WB-PATH-006: {operation} 必须位于已校验的 compiled 事务目录。{full}");
        }

        EnsureNoReparsePoint(full);
        return full;
    }
    public string RequireExport(string? candidate = null, string operation = "export") => RequireWithin(candidate ?? ExportRoot, ExportRoot, operation, false);
    public string RequirePrebatches(string candidate, string operation = "prebatch") => RequireWithin(candidate, PrebatchesRoot, operation, true);
    public string RequireBatches(string candidate, string operation = "batch") => RequireWithin(candidate, BatchesRoot, operation, true);
    public string RequireCache(string candidate, string operation = "cache") => RequireWithin(candidate, CacheRoot, operation, true);

    public string RequireAllowed(string candidate, string operation)
    {
        var full = Path.GetFullPath(candidate);
        var allowed = new[] { AuthoringRoot, FixturesRoot, CompiledRoot, Path.Combine(Root, "export"), PrebatchesRoot, BatchesRoot, CacheRoot };
        if (!allowed.Any(x => WorkspaceRootGuard.IsSameOrInside(full, x)))
        {
            throw new InvalidOperationException($"WB-PATH-002: {operation} 只允许写入 authoring/fixtures/compiled/export/prebatches/batches/cache。{full}");
        }
        EnsureNoReparsePoint(full);
        return full;
    }

    private string RequireWithin(string? candidate, string allowedRoot, string operation, bool allowRoot)
    {
        var full = Path.GetFullPath(candidate ?? allowedRoot);
        if ((!allowRoot && full.Equals(Path.GetFullPath(Path.Combine(Root, "export")), StringComparison.OrdinalIgnoreCase))
            || !WorkspaceRootGuard.IsSameOrInside(full, allowedRoot))
        {
            throw new InvalidOperationException($"WB-PATH-003: {operation} 目标必须位于 {Path.GetRelativePath(Root, allowedRoot).Replace('\\', '/')}。{full}");
        }

        var relative = Path.GetRelativePath(Root, full);
        var parts = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var deniedNames = new[] { "src", "AWAKE.csproj", "SubModule.xml", "Modules", "ModuleData", "dist", "_build_out", "candidate_frozen", "pending_game" };
        if (parts.Any(part => deniedNames.Contains(part, StringComparer.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"WB-PATH-004: {operation} 命中禁止路径。{full}");
        }

        EnsureNoReparsePoint(full);
        return full;
    }

    private static void EnsureNoReparsePoint(string path)
    {
        var current = path;
        while (!string.IsNullOrEmpty(current) && current.Length >= Path.GetPathRoot(current)!.Length)
        {
            if ((Directory.Exists(current) || File.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidOperationException($"WB-PATH-005: 禁止经过 reparse point。{current}");
            }
            current = Path.GetDirectoryName(current)!;
        }
    }
}

public sealed class WorkspaceService
{
    private readonly SafeYamlLoader _yaml;
    private readonly SchemaValidator _schema;
    private readonly TaxonomyCatalogService _taxonomy;
    private readonly object _authoringWriteGate = new();
    public WorkspaceWritePolicy Policy { get; }
    public string Root { get; }
    public string SchemaRoot { get; }
    public WorkspaceReadProbe? ReadProbe { get; }
    public TaxonomyCatalogService Taxonomy => _taxonomy;

    public WorkspaceService(WorkspaceOptions options)
    {
        Root = Path.GetFullPath(options.Root);
        SchemaRoot = Path.GetFullPath(options.SchemaRoot);
        ReadProbe = options.ReadProbe ?? new WorkspaceReadProbe();
        WorkspaceRootGuard.Validate(Root, SchemaRoot);
        WorkspacePathPolicy.ValidateWorkspaceRoot(Root, schemaRoot: SchemaRoot);
        Policy = new WorkspaceWritePolicy(Root);
        _yaml = new SafeYamlLoader();
        _schema = new SchemaValidator();
        _taxonomy = new TaxonomyCatalogService(SchemaRoot, _schema);
    }

    public void Initialize()
    {
        foreach (var directory in new[] { "authoring", "authoring/sources", "authoring/audit/events", "authoring/identity", "authoring/suggestions", "authoring/draft-state", "authoring/session-state", "compiled", "compiled/reports", "export/WorldbookV2", "fixtures", "prebatches", "prebatches/create-reservations", "batches", "cache/v1" })
        {
            Directory.CreateDirectory(Policy.RequireAllowed(Path.Combine(Root, directory), "初始化"));
        }
    }

    public IReadOnlyList<(string Path, JsonObject Document, ValidationReport Report)> LoadDocuments(SnapshotInputStore? snapshot = null)
    {
        return LoadAuthoringDocuments(snapshot).Select(x => (x.AbsolutePath, x.Document, x.Report)).ToList();
    }

    public IReadOnlyList<AuthoringDocumentFile> LoadAuthoringDocuments(SnapshotInputStore? snapshot = null)
    {
        var paths = snapshot?.AuthoringPaths ?? EnumerateAuthoringPaths();
        return paths.Select(path => ReadAuthoring(path, snapshot)).ToList();
    }

    public AuthoringDocumentFile ReadAuthoring(string path)
        => ReadAuthoring(path, null);

    public AuthoringDocumentFile ReadAuthoring(string path, SnapshotInputStore? snapshot)
    {
        var target = ResolveAuthoringPath(path, "读取 authoring");
        if (!File.Exists(target)) throw new InvalidOperationException($"WB-DOC-404: authoring 文档不存在。{path}");

        var bytes = snapshot?.GetBytes(target) ?? File.ReadAllBytes(target);
        var content = System.Text.Encoding.UTF8.GetString(bytes);
        return ParseAuthoringContent(target, content, Hashing.Sha256Bytes(bytes), snapshot);
    }

    public AuthoringDocumentFile ValidateAuthoringContent(string path, string content)
    {
        var target = ResolveAuthoringPath(path, "校验 authoring");
        if (content is null) throw new InvalidOperationException("WB-AUTHORING-SAVE-400: authoring 内容不能为空。");
        var bytes = new System.Text.UTF8Encoding(false).GetBytes(content);
        return ParseAuthoringContent(target, content, Hashing.Sha256Bytes(bytes), null);
    }

    private AuthoringDocumentFile ParseAuthoringContent(string target, string content, string inputHash, SnapshotInputStore? snapshot)
    {
        var report = new ValidationReport { InputHash = inputHash };
        var document = ParseAuthoring(target, content, report);
        var schema = Path.Combine(SchemaRoot, "awake.worldbook.authoring.v1.schema.json");
        if (File.Exists(schema)) _schema.Validate(document, schema, report, snapshot);
        _taxonomy.Load(snapshot).ValidateDocument(document, report, requireSubdomain: false);
        snapshot?.SetParsed(target, document);
        return new AuthoringDocumentFile(
            RelativePath(target),
            target,
            FormatFor(target),
            content,
            document,
            report);
    }

    public IReadOnlyList<(string Path, JsonObject Document, ValidationReport Report)> LoadSourceRegistries(SnapshotInputStore? snapshot = null)
    {
        var directory = Path.Combine(Root, "authoring", "sources");
        if (!Directory.Exists(directory)) return [];
        return Directory.EnumerateFiles(directory, "*.yaml", SearchOption.TopDirectoryOnly)
            .Concat(Directory.EnumerateFiles(directory, "*.yml", SearchOption.TopDirectoryOnly))
            .Concat(Directory.EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly))
            .Where(x => !Path.GetFileName(x).Equals("source-demo.txt", StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .Select(path =>
            {
                var bytes = snapshot?.GetBytes(path) ?? File.ReadAllBytes(path);
                var report = new ValidationReport { InputHash = Hashing.Sha256Bytes(bytes) };
                JsonObject doc;
                if (Path.GetExtension(path).Equals(".json", StringComparison.OrdinalIgnoreCase))
                {
                    try { doc = JsonNode.Parse(bytes)?.AsObject() ?? new JsonObject(); }
                    catch (Exception ex) { doc = new JsonObject(); report.Error("WB-SOURCE-000", "来源登记 JSON 无法解析。", path, ex.Message); }
                }
                else doc = _yaml.Load(path, bytes, report);
                var schema = Path.Combine(SchemaRoot, "source.registry.v1.schema.json");
                if (File.Exists(schema)) _schema.Validate(doc, schema, report, snapshot);
                snapshot?.SetParsed(path, doc);
                return (path, doc, report);
            }).ToList();
    }

    public IReadOnlyList<string> EnumerateSnapshotInputPaths(IEnumerable<string>? explicitAuthoringPaths = null)
    {
        var authoringPaths = explicitAuthoringPaths ?? EnumerateAuthoringPaths();
        var paths = authoringPaths
            .Concat(EnumerateSourceInputFiles())
            .Concat(EnumerateAuditFiles())
            .Concat(EnumerateLedgerFiles())
            .Concat(EnumerateSchemaInputFiles())
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return paths;
    }

    public string SnapshotInputCategory(string path)
    {
        var full = Path.GetFullPath(path);
        if (WorkspaceRootGuard.IsSameOrInside(full, Path.Combine(Root, "authoring", "sources"))) return "source";
        if (WorkspaceRootGuard.IsSameOrInside(full, Path.Combine(Root, "authoring", "audit"))) return "audit";
        if (WorkspaceRootGuard.IsSameOrInside(full, Path.Combine(Root, "authoring", "identity"))) return "ledger";
        if (WorkspaceRootGuard.IsSameOrInside(full, SchemaRoot)) return "schema";
        return "authoring";
    }

    public IEnumerable<string> EnumerateAuditFiles() => EnumerateFiles(Path.Combine(Root, "authoring", "audit", "events"), "*.jsonl");
    public IEnumerable<string> EnumerateLedgerFiles() => EnumerateFiles(Path.Combine(Root, "authoring", "identity"), "*.jsonl");

    private static IEnumerable<string> EnumerateFiles(string directory, string pattern)
        => Directory.Exists(directory) ? Directory.EnumerateFiles(directory, pattern, SearchOption.TopDirectoryOnly).OrderBy(x => x, StringComparer.OrdinalIgnoreCase) : [];

    public void SeedSample(string schemaRoot)
    {
        var fixture = Path.Combine(schemaRoot, "fixture-valid-minimal.yaml");
        var target = Path.Combine(Root, "authoring", "demo.yaml");
        if (File.Exists(fixture) && !File.Exists(target)) File.Copy(fixture, target);
        var sourceRegistry = Path.Combine(schemaRoot, "source-fixture-demo.yaml");
        var sourceRegistryTarget = Path.Combine(Root, "authoring", "sources", "source-demo.yaml");
        if (File.Exists(sourceRegistry) && !File.Exists(sourceRegistryTarget)) File.Copy(sourceRegistry, sourceRegistryTarget);
        var sourceText = Path.Combine(schemaRoot, "source-demo.txt");
        var sourceTarget = Path.Combine(Root, "authoring", "sources", "source-demo.txt");
        if (File.Exists(sourceText) && !File.Exists(sourceTarget)) File.Copy(sourceText, sourceTarget);
    }
    public string SaveAuthoring(string relativePath, string content)
    {
        var target = ResolveAuthoringPath(relativePath, "保存 authoring");
        if (!target.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase) && !target.EndsWith(".yml", StringComparison.OrdinalIgnoreCase) && !target.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("WB-SAVE-001: authoring 只允许保存 YAML 或 JSON 文件。");
        }
        lock (_authoringWriteGate) return WriteAuthoringTarget(target, content);
    }

    public string SaveAuthoringIfUnchanged(string relativePath, string content, string expectedHash)
    {
        var target = ResolveAuthoringPath(relativePath, "CAS 保存 authoring");
        if (!target.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase) && !target.EndsWith(".yml", StringComparison.OrdinalIgnoreCase) && !target.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("WB-SAVE-001: authoring 只允许保存 YAML 或 JSON 文件。");
        }
        if (string.IsNullOrWhiteSpace(expectedHash)) throw new InvalidOperationException("WB-CAS-400: 源文档 hash 不能为空。");

        lock (_authoringWriteGate)
        {
            if (!File.Exists(target)) throw new InvalidOperationException($"WB-CAS-409: authoring 文档不存在。{relativePath}");
            var actualHash = Hashing.FileSha256(target);
            if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("WB-CAS-409: 档案已被其他编辑操作更新，请重新读取后再保存。");
            return WriteAuthoringTarget(target, content);
        }
    }

    public string SaveAuthoringIfUnchanged(string relativePath, string content, string expectedHash, int expectedRevision)
    {
        var target = ResolveAuthoringPath(relativePath, "CAS 保存 authoring");
        if (!target.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase) && !target.EndsWith(".yml", StringComparison.OrdinalIgnoreCase) && !target.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("WB-AUTHORING-SAVE-400: authoring 只允许保存 YAML 或 JSON 文件。");
        if (string.IsNullOrWhiteSpace(expectedHash) || expectedRevision < 1)
            throw new InvalidOperationException("WB-AUTHORING-SAVE-400: 高级保存缺少有效的源 hash 或 revision。");

        lock (_authoringWriteGate)
        {
            if (!File.Exists(target)) throw new InvalidOperationException("WB-AUTHORING-CAS-409: 档案已经不存在，未覆盖磁盘内容。");
            var actualBytes = File.ReadAllBytes(target);
            var actualHash = Hashing.Sha256Bytes(actualBytes);
            if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("WB-AUTHORING-CAS-409: 档案已被其他编辑操作更新，未覆盖外部版本。");

            var currentContent = System.Text.Encoding.UTF8.GetString(actualBytes);
            var currentReport = new ValidationReport { InputHash = actualHash };
            var current = ParseAuthoring(target, currentContent, currentReport);
            var actualRevision = WorldbookInputNormalization.ReadRevision(current);
            if (actualRevision != expectedRevision)
                throw new InvalidOperationException("WB-AUTHORING-CAS-409: 档案 revision 已变化，未覆盖外部版本。");
            return WriteAuthoringTarget(target, content);
        }
    }

    private string WriteAuthoringTarget(string target, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        WorkspacePathPolicy.EnsureNoReparsePoint(Path.GetDirectoryName(target)!);
        var temp = target + $".{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(temp, content, new System.Text.UTF8Encoding(false));
            WorkspacePathPolicy.EnsureNoReparsePoint(Path.GetDirectoryName(target)!);
            File.Move(temp, target, true);
        }
        finally
        {
            try { if (File.Exists(temp)) File.Delete(temp); } catch { }
        }
        return target;
    }

    private IEnumerable<string> EnumerateAuthoringPaths()
    {
        var directory = Path.Combine(Root, "authoring");
        if (!Directory.Exists(directory)) return [];
        return Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .Where(IsAuthoringDocument)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase);
    }

    private IEnumerable<string> EnumerateSourceInputFiles()
    {
        var directory = Path.Combine(Root, "authoring", "sources");
        return Directory.Exists(directory)
            ? Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly).OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            : [];
    }

    private IEnumerable<string> EnumerateSchemaInputFiles()
    {
        var names = new[]
        {
            "awake.worldbook.authoring.v1.schema.json",
            "knowledge-taxonomy.v1.json",
            "knowledge-taxonomy.v1.schema.json",
            "source.registry.v1.schema.json",
            "profile-registry.v1.json",
            "referral-registry.v1.json",
            "profile-registry.v1.schema.json",
            "referral-registry.v1.schema.json",
            // 互引边表（2026-09-20）：**编译输入**，不是审计附件。
            // ⚠️ 必须进这张名单 —— 快照有「声明的输入闭包 == 实际读入清单」这条不变量（测试 F73），
            //    编译器读了它却没声明，那条断言就会红。反之文件不存在时 `Where(File.Exists)` 自动跳过。
            "link-registry.v1.json",
            "link-registry.v1.schema.json",
            "audit-event.v1.schema.json",
            "id-ledger.v1.schema.json",
            "content-graph.v1.schema.json",
            "preview-fixture.v1.schema.json",
            "current-pointer.v1.schema.json"
        };
        return names.Select(name => Path.Combine(SchemaRoot, name)).Where(File.Exists);
    }

    private static bool IsAuthoringDocument(string path)
    {
        var relative = Path.GetRelativePath(Path.Combine(Path.GetDirectoryName(path)!, ".."), path);
        var extension = Path.GetExtension(path);
        if (extension is not (".yaml" or ".yml" or ".json")) return false;
        return !relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(x => x.Equals("sources", StringComparison.OrdinalIgnoreCase)
                || x.Equals("suggestions", StringComparison.OrdinalIgnoreCase)
                || x.Equals("audit", StringComparison.OrdinalIgnoreCase)
                || x.Equals("identity", StringComparison.OrdinalIgnoreCase)
                || x.Equals("draft-state", StringComparison.OrdinalIgnoreCase)
                || x.Equals("session-state", StringComparison.OrdinalIgnoreCase));
    }

    private string ResolveAuthoringPath(string path, string operation)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new InvalidOperationException($"WB-PATH-001: {operation} 路径不能为空。");
        var candidate = Path.IsPathRooted(path)
            ? path
            : path.StartsWith("authoring", StringComparison.OrdinalIgnoreCase)
                ? Path.Combine(Root, path)
                : Path.Combine(Root, "authoring", path);
        return Policy.RequireAuthoring(candidate, operation);
    }

    private JsonObject ParseAuthoring(string path, string content, ValidationReport report)
    {
        if (Path.GetExtension(path).Equals(".json", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                return JsonNode.Parse(content)?.AsObject() ?? new JsonObject();
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException)
            {
                report.Error("WB-JSON-001", "JSON 文档无法解析。", RelativePath(path), ex.Message);
                return new JsonObject();
            }
        }

        return _yaml.Load(path, System.Text.Encoding.UTF8.GetBytes(content), report);
    }

    private string RelativePath(string path) => Path.GetRelativePath(Root, path).Replace('\\', '/');
    private static string FormatFor(string path) => Path.GetExtension(path).Equals(".json", StringComparison.OrdinalIgnoreCase) ? "json" : "yaml";
}

public sealed record AuthoringDocumentFile(
    string Path,
    string AbsolutePath,
    string Format,
    string Content,
    JsonObject Document,
    ValidationReport Report);







