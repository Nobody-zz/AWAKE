using System.Text.Json;
using System.Text.Json.Nodes;

namespace Awake.WorldbookStudio.Core;

public sealed record TaxonomySubdomain(
    string Id,
    string Label,
    string Help,
    IReadOnlyList<string> Examples);

public sealed record TaxonomyDomain(
    string Id,
    string Label,
    string Help,
    IReadOnlyList<string> Examples,
    IReadOnlyList<string> ConflictHints,
    IReadOnlyList<TaxonomySubdomain> Subdomains);

public sealed record TaxonomyCatalog(
    string SchemaVersion,
    string Version,
    string Hash,
    IReadOnlyList<TaxonomyDomain> Domains)
{
    private readonly IReadOnlyDictionary<string, int> _domainOrder =
        Domains.Select((domain, index) => (domain.Id, index)).ToDictionary(x => x.Id, x => x.index, StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> DomainOrder => _domainOrder;
    public IReadOnlySet<string> DomainIds => _domainOrder.Keys.ToHashSet(StringComparer.Ordinal);

    public void ValidateDocument(JsonObject document, ValidationReport report, bool requireSubdomain = false)
    {
        var domain = Text(document["domain"]);
        if (!TryGetDomain(domain, out var domainInfo))
        {
            report.Error("WB-DOC-002", "请选择一个有效的知识分类。", "domain", "可选：政治、经济、文化、战争、地理。");
            return;
        }

        var hasSubdomain = document.ContainsKey("subdomain");
        var subdomain = Text(document["subdomain"]);
        if (hasSubdomain && string.IsNullOrWhiteSpace(subdomain))
        {
            report.Error("WB-TAXONOMY-422", "二级主题不能是空白。", "subdomain", "请选择当前分类下的一个二级主题，或删除该字段以保留旧档案兼容状态。");
        }
        else if (!string.IsNullOrWhiteSpace(subdomain) && !domainInfo.Subdomains.Any(item => string.Equals(item.Id, subdomain, StringComparison.Ordinal)))
        {
            report.Error("WB-TAXONOMY-422", "二级主题与主分类不匹配。", "subdomain", $"“{domainInfo.Label}”下没有这个二级主题。");
        }
        else if (requireSubdomain && string.IsNullOrWhiteSpace(subdomain))
        {
            report.Error("WB-TAXONOMY-422", "新档案必须选择二级主题。", "subdomain", $"请在“{domainInfo.Label}”分类下选择一个二级主题。");
        }

        if (!document.ContainsKey("related_domains")) return;
        if (document["related_domains"] is not JsonArray related)
        {
            report.Error("WB-TAXONOMY-422", "相关分类必须是列表。", "related_domains", "请使用编辑器中的相关分类多选，不要手写单个值。");
            return;
        }

        if (related.Count == 0)
        {
            report.Error("WB-TAXONOMY-422", "相关分类不能为空列表。", "related_domains", "没有相关分类时请删除这一项，不要保留空列表。");
            return;
        }
        if (related.Count > 4)
            report.Error("WB-TAXONOMY-422", "相关分类最多选择四项。", "related_domains");

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var previousOrder = -1;
        foreach (var node in related)
        {
            var relatedDomain = Text(node);
            if (!TryGetDomain(relatedDomain, out _))
            {
                report.Error("WB-TAXONOMY-422", "相关分类中包含未知分类。", "related_domains", relatedDomain ?? "空值");
                continue;
            }
            if (relatedDomain == domain)
                report.Error("WB-TAXONOMY-422", "相关分类不能重复选择主分类。", "related_domains", domain);
            if (!seen.Add(relatedDomain!))
                report.Error("WB-TAXONOMY-422", "相关分类不能重复。", "related_domains", relatedDomain);
            if (DomainOrder[relatedDomain!] < previousOrder)
                report.Error("WB-TAXONOMY-422", "相关分类必须按目录顺序保存。", "related_domains", "请重新选择后保存。");
            previousOrder = DomainOrder[relatedDomain!];
        }
    }

    public bool TryGetDomain(string? id, out TaxonomyDomain domain)
    {
        if (!string.IsNullOrWhiteSpace(id))
        {
            var found = Domains.FirstOrDefault(item => string.Equals(item.Id, id, StringComparison.Ordinal));
            if (found is not null)
            {
                domain = found;
                return true;
            }
        }

        domain = null!;
        return false;
    }

    public JsonObject ToCatalogJson()
    {
        var domains = new JsonArray();
        foreach (var domain in Domains)
        {
            var subdomains = new JsonArray();
            foreach (var subdomain in domain.Subdomains)
            {
                subdomains.Add(new JsonObject
                {
                    ["value"] = subdomain.Id,
                    ["id"] = subdomain.Id,
                    ["label"] = subdomain.Label,
                    ["help"] = subdomain.Help,
                    ["examples"] = StringArray(subdomain.Examples)
                });
            }

            domains.Add(new JsonObject
            {
                ["value"] = domain.Id,
                ["id"] = domain.Id,
                ["label"] = domain.Label,
                ["help"] = domain.Help,
                ["examples"] = StringArray(domain.Examples),
                ["conflictHints"] = StringArray(domain.ConflictHints),
                ["subdomains"] = subdomains
            });
        }

        return new JsonObject
        {
            ["schemaVersion"] = SchemaVersion,
            ["taxonomyVersion"] = Version,
            ["taxonomyHash"] = Hash,
            ["domains"] = domains
        };
    }

    private static JsonArray StringArray(IEnumerable<string> values)
        => new(values.Select(value => (JsonNode)JsonValue.Create(value)!).ToArray());

    private static string? Text(JsonNode? node) => node?.GetValue<string>();
}

public sealed class TaxonomyCatalogService
{
    public const string SupportedVersion = "1.0.0";
    public const string TaxonomyFileName = "knowledge-taxonomy.v1.json";
    public const string TaxonomySchemaFileName = "knowledge-taxonomy.v1.schema.json";
    public const string SchemaVersion = "awake.worldbook.knowledge-taxonomy.v1";

    private readonly string _schemaRoot;
    private readonly SchemaValidator _schema;
    private readonly object _gate = new();
    private string? _cachedHash;
    private TaxonomyCatalog? _cached;

    public TaxonomyCatalogService(string schemaRoot, SchemaValidator? schema = null)
    {
        _schemaRoot = Path.GetFullPath(schemaRoot);
        _schema = schema ?? new SchemaValidator();
    }

    public string TaxonomyPath => Path.Combine(_schemaRoot, TaxonomyFileName);
    public string TaxonomySchemaPath => Path.Combine(_schemaRoot, TaxonomySchemaFileName);

    public TaxonomyCatalog Load(SnapshotInputStore? snapshot = null)
    {
        byte[] bytes;
        try
        {
            bytes = snapshot?.GetBytes(TaxonomyPath) ?? File.ReadAllBytes(TaxonomyPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FileNotFoundException)
        {
            throw TaxonomyFailure("分类目录文件缺失或无法读取。", ex.Message);
        }

        var hash = Hashing.Sha256Bytes(bytes).ToLowerInvariant();
        lock (_gate)
        {
            if (_cached is not null && string.Equals(_cachedHash, hash, StringComparison.Ordinal)) return _cached;
        }

        JsonObject document;
        try
        {
            document = JsonNode.Parse(bytes)?.AsObject() ?? throw new JsonException("分类目录必须是 JSON 对象。");
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            throw TaxonomyFailure("分类目录 JSON 无法解析。", ex.Message);
        }

        if (!File.Exists(TaxonomySchemaPath) && snapshot is null)
            throw TaxonomyFailure("分类目录 Schema 缺失。", TaxonomySchemaPath);

        var schemaReport = new ValidationReport();
        try
        {
            _schema.Validate(document, TaxonomySchemaPath, schemaReport, snapshot);
        }
        catch (Exception ex)
        {
            throw TaxonomyFailure("分类目录 Schema 无法执行。", ex.Message);
        }

        if (!schemaReport.Valid)
            throw TaxonomyFailure("分类目录未通过结构检查。", string.Join("; ", schemaReport.Diagnostics.Select(x => x.Message)));

        var catalog = ParseCatalog(document, hash);
        lock (_gate)
        {
            _cachedHash = hash;
            _cached = catalog;
            return catalog;
        }
    }

    public void EnsureSnapshot(string? version, string? hash)
    {
        if (string.IsNullOrWhiteSpace(version) || string.IsNullOrWhiteSpace(hash))
            throw new InvalidOperationException("WB-TAXONOMY-CAS-400: 分类目录快照无效，请重新读取档案。");
        var current = Load();
        if (!string.Equals(version, current.Version, StringComparison.Ordinal)
            || !string.Equals(hash, current.Hash, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"WB-TAXONOMY-CAS-409: 分类目录已更新，请刷新后再保存。taxonomyVersion={current.Version};taxonomyHash={current.Hash}");
        }
    }

    private TaxonomyCatalog ParseCatalog(JsonObject document, string hash)
    {
        var schemaVersion = Text(document["schema_version"]);
        var version = Text(document["taxonomy_version"]);
        if (!string.Equals(schemaVersion, SchemaVersion, StringComparison.Ordinal) || !string.Equals(version, SupportedVersion, StringComparison.Ordinal))
            throw TaxonomyFailure("分类目录版本不受支持。", $"schema_version={schemaVersion};taxonomy_version={version}");

        var domains = new List<TaxonomyDomain>();
        var domainIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in document["domains"]?.AsArray() ?? throw TaxonomyFailure("分类目录缺少主分类。", "domains"))
        {
            if (node is not JsonObject domain) throw TaxonomyFailure("分类目录主分类格式无效。", "domains[]");
            var id = Text(domain["id"]);
            if (string.IsNullOrWhiteSpace(id) || !domainIds.Add(id)) throw TaxonomyFailure("分类目录主分类重复或为空。", id ?? "domains[]");
            var subdomains = new List<TaxonomySubdomain>();
            var subdomainIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var child in domain["subdomains"]?.AsArray() ?? [])
            {
                if (child is not JsonObject subdomain) throw TaxonomyFailure("分类目录二级主题格式无效。", id);
                var subdomainId = Text(subdomain["id"]);
                if (string.IsNullOrWhiteSpace(subdomainId) || !subdomainIds.Add(subdomainId)) throw TaxonomyFailure("分类目录二级主题重复或为空。", $"{id}.{subdomainId}");
                subdomains.Add(new TaxonomySubdomain(subdomainId, LocalizedText(subdomain["label"], $"{id}.{subdomainId}.label"), LocalizedText(subdomain["help"], $"{id}.{subdomainId}.help"), LocalizedList(subdomain["examples"])));
            }
            domains.Add(new TaxonomyDomain(id, LocalizedText(domain["label"], id + ".label"), LocalizedText(domain["help"], id + ".help"), LocalizedList(domain["examples"]), LocalizedList(domain["conflict_hints"]), subdomains));
        }

        var expected = new[] { "politics", "economy", "culture", "war", "geography" };
        if (!domains.Select(x => x.Id).SequenceEqual(expected, StringComparer.Ordinal))
            throw TaxonomyFailure("分类目录主分类顺序或数量不正确。", string.Join(",", domains.Select(x => x.Id)));
        if (domains.Any(x => x.Subdomains.Count == 0)) throw TaxonomyFailure("分类目录存在没有二级主题的主分类。", "subdomains");
        return new TaxonomyCatalog(schemaVersion!, version!, hash, domains);
    }

    private static JsonArray StringArray(IEnumerable<string> values)
        => new(values.Select(value => JsonValue.Create(value) as JsonNode).ToArray()!);

    private static string LocalizedText(JsonNode? node, string path)
        => node is JsonObject localized && Text(localized["zh-CN"]) is { Length: > 0 } value
            ? value
            : throw TaxonomyFailure("分类目录缺少中文说明。", path);

    private static IReadOnlyList<string> LocalizedList(JsonNode? node)
        => node is JsonObject localized
            ? localized["zh-CN"]?.AsArray().Select(Text).Where(x => !string.IsNullOrWhiteSpace(x)).Cast<string>().ToArray() ?? Array.Empty<string>()
            : Array.Empty<string>();

    private static string? Text(JsonNode? node)
        => node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    private static InvalidOperationException TaxonomyFailure(string message, string detail)
        => new($"WB-TAXONOMY-500: {message} {detail}");
}
