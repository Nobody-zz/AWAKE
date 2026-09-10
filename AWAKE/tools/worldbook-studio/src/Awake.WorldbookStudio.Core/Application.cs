using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using YamlDotNet.Serialization;

namespace Awake.WorldbookStudio.Core;

public sealed class WorldbookApplicationService
{
    private readonly WorkspaceService _workspace;
    private readonly SchemaValidator _schema = new();
    private readonly RegistryService _registries;
    private readonly EntityCatalogService _entities;
    private readonly SourceRegistryService _sources;
    private readonly AuditLedgerService _audit;

    public WorldbookApplicationService(WorkspaceService workspace)
    {
        _workspace = workspace;
        _registries = new RegistryService(workspace, _schema);
        _entities = new EntityCatalogService(workspace, _schema);
        _sources = new SourceRegistryService(workspace, _schema);
        _audit = new AuditLedgerService(workspace, _schema);
    }

    public ValidationReport Validate() => BuildSnapshot().Report;

    public string GenerateConfirmationToken(string contentTier = "adult_optional")
    {
        var snapshot = BuildSnapshot();
        return ConfirmationToken(snapshot, contentTier);
    }

    public void Initialize(bool seedSample = false)
    {
        _workspace.Initialize();
        if (seedSample) _workspace.SeedSample(_workspace.SchemaRoot);
    }
    public string SaveAuthoring(string relativePath, string content) => _workspace.SaveAuthoring(relativePath, content);

    public IReadOnlyList<AuthoringDocumentSummary> ListDocuments()
    {
        return _workspace.LoadAuthoringDocuments()
            .Select(x => new AuthoringDocumentSummary(
                x.Path,
                x.Format,
                x.Document["id"]?.GetValue<string>(),
                x.Document["title"] is JsonObject title ? Localized(title) : null,
                x.Document["domain"]?.GetValue<string>(),
                x.Document["status"]?.GetValue<string>(),
                x.Document["content_tier"]?.GetValue<string>(),
                IntegerValue(x.Document["revision"]),
                x.Report.Valid,
                x.Report.Diagnostics))
            .ToList();
    }

    public string FormatAuthoringDocument(JsonObject document, string path)
    {
        ArgumentNullException.ThrowIfNull(document);
        return Path.GetExtension(path).Equals(".json", StringComparison.OrdinalIgnoreCase)
            ? document.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine
            : SerializeYaml(document);
    }
    public AuthoringDocumentFile ReadDocument(string path) => _workspace.ReadAuthoring(path);

    public JsonObject ReadEditorDocument(string path)
    {
        var taxonomy = _workspace.Taxonomy.Load();
        var file = ReadDocument(path);
        var registryReport = new ValidationReport();
        var registries = _registries.LoadAndValidate(registryReport);
        return AuthoringEditorProjection.Project(file, registries, registryReport, taxonomy);
    }

    public JsonObject ProjectEditorBuffer(AuthoringDocumentFile source, JsonObject document)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(document);
        var taxonomy = _workspace.Taxonomy.Load();
        var registryReport = new ValidationReport();
        var registries = _registries.LoadAndValidate(registryReport);
        var buffer = source with { Document = document };
        return AuthoringEditorProjection.Project(buffer, registries, registryReport, taxonomy);
    }

    public JsonObject SaveEditorDocument(string path, JsonObject model, string sourceHash, int expectedRevision, JsonObject registryBinding, string? taxonomyVersion = null, string? taxonomyHash = null)
    {
        if (model is null) throw new InvalidOperationException("WB-EDITOR-SAVE-400: 作者模式模型不能为空。");
        if (string.IsNullOrWhiteSpace(sourceHash)) throw new InvalidOperationException("WB-CAS-400: 源文档 hash 不能为空。");
        if (expectedRevision < 1) throw new InvalidOperationException("WB-CAS-400: 文档 revision 无效。");
        if (taxonomyVersion is not null || taxonomyHash is not null) _workspace.Taxonomy.EnsureSnapshot(taxonomyVersion, taxonomyHash);
        var taxonomy = _workspace.Taxonomy.Load();

        var current = ReadDocument(path);
        var actualHash = current.Report.InputHash ?? Hashing.FileSha256(current.AbsolutePath);
        if (!string.Equals(actualHash, sourceHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("WB-CAS-409: 档案已被其他编辑操作更新，请重新读取后再保存。");

        var actualRevision = IntegerValue(current.Document["revision"]) ?? 1;
        if (actualRevision != expectedRevision)
            throw new InvalidOperationException("WB-CAS-409: 档案 revision 已变化，请重新读取后再保存。");

        var registryReport = new ValidationReport();
        var registries = _registries.LoadAndValidate(registryReport);
        if (!registryReport.Valid)
            throw new InvalidOperationException("WB-REGISTRY-422: 身份或推荐对象注册表当前不可用。");
        EnsureRegistryBinding(registryBinding, registries);

        var candidate = AuthoringEditorProjection.Merge(current.Document, model, registries, taxonomy);
        var candidateReport = new ValidationReport();
        _schema.Validate(candidate, Path.Combine(_workspace.SchemaRoot, "awake.worldbook.authoring.v1.schema.json"), candidateReport);
        if (!candidateReport.Valid)
        {
            var first = candidateReport.Diagnostics.FirstOrDefault(x => x.Severity == "error");
            throw new InvalidOperationException($"WB-EDITOR-SCHEMA-422: {first?.Message ?? "作者模式内容未通过结构检查。"}");
        }

        var content = FormatAuthoringDocument(candidate, current.Path);
        _workspace.SaveAuthoringIfUnchanged(path, content, sourceHash, expectedRevision);
        return ReadEditorDocument(path);
    }

    public AuthoringDocumentFile SaveAndValidate(string path, string content)
    {
        SaveAuthoring(path, content);
        return ReadDocument(path);
    }

    public AuthoringDocumentFile SaveAdvancedDocument(string path, string content, string sourceHash, int expectedRevision)
        => SaveAdvancedDocumentWithEvidence(path, content, sourceHash, expectedRevision).Document;

    internal AdvancedSaveResult SaveAdvancedDocumentWithEvidence(string path, string content, string sourceHash, int expectedRevision)
    {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(content))
            throw new AuthoringSaveFailureException("WB-AUTHORING-SAVE-400", 400, "高级保存需要有效的档案路径和内容。" );
        if (string.IsNullOrWhiteSpace(sourceHash) || expectedRevision < 1)
            throw new AuthoringSaveFailureException("WB-AUTHORING-SAVE-400", 400, "高级保存缺少有效的源 hash 或 revision。" );

        AuthoringDocumentFile candidate;
        try
        {
            candidate = _workspace.ValidateAuthoringContent(path, content);
        }
        catch (InvalidOperationException error) when (error.Message.StartsWith("WB-PATH-", StringComparison.Ordinal) || error.Message.StartsWith("WB-AUTHORING-SAVE-400:", StringComparison.Ordinal))
        {
            throw new AuthoringSaveFailureException("WB-AUTHORING-SAVE-400", 400, "高级保存路径或请求无效。", [new Diagnostic("WB-AUTHORING-SAVE-400", "error", error.Message, path)]);
        }
        catch (ArgumentException error)
        {
            throw new AuthoringSaveFailureException("WB-AUTHORING-SAVE-400", 400, "高级保存路径或请求无效。", [new Diagnostic("WB-AUTHORING-SAVE-400", "error", error.Message, path)]);
        }
        catch (NotSupportedException error)
        {
            throw new AuthoringSaveFailureException("WB-AUTHORING-SAVE-400", 400, "高级保存路径或请求无效。", [new Diagnostic("WB-AUTHORING-SAVE-400", "error", error.Message, path)]);
        }
        catch (InvalidOperationException error)
        {
            throw new AuthoringSaveFailureException("WB-AUTHORING-PARSE-422", 422, "高级文件无法解析，请检查 YAML 或 JSON 格式。", [new Diagnostic("WB-AUTHORING-PARSE-422", "error", error.Message, path)]);
        }

        if (!candidate.Report.Valid)
            throw CandidateSaveFailure(candidate, path);

        var current = ReadDocument(path);
        var actualHash = current.Report.InputHash ?? Hashing.FileSha256(current.AbsolutePath);
        var actualRevision = IntegerValue(current.Document["revision"]) ?? 0;
        if (!string.Equals(actualHash, sourceHash, StringComparison.OrdinalIgnoreCase) || actualRevision != expectedRevision)
            throw new AuthoringSaveFailureException("WB-AUTHORING-CAS-409", 409, "档案已经被其他操作更新，未覆盖外部版本。", [new Diagnostic("WB-AUTHORING-CAS-409", "error", "当前磁盘版本与编辑器打开时的版本不一致。", path)]);

        var writtenDocument = JsonNode.Parse(candidate.Document.ToJsonString())!.AsObject();
        writtenDocument["revision"] = actualRevision + 1;
        var writtenContent = FormatAuthoringDocument(writtenDocument, current.Path);
        var expectedContentHash = Hashing.Sha256Text(writtenContent);
        try
        {
            _workspace.SaveAuthoringIfUnchanged(path, writtenContent, sourceHash, expectedRevision);
        }
        catch (AuthoringSaveFailureException)
        {
            throw;
        }
        catch (InvalidOperationException error) when (error.Message.StartsWith("WB-AUTHORING-CAS-409:", StringComparison.Ordinal))
        {
            throw new AuthoringSaveFailureException("WB-AUTHORING-CAS-409", 409, "档案已经被其他操作更新，未覆盖外部版本。", [new Diagnostic("WB-AUTHORING-CAS-409", "error", "当前磁盘版本与编辑器打开时的版本不一致。", path)]);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            throw new AuthoringSaveFailureException("WB-AUTHORING-UNKNOWN-503", 503, "文件写入结果暂时无法确认，请点击“检查保存结果”。", [new Diagnostic("WB-AUTHORING-UNKNOWN-503", "warning", "写入过程中发生文件系统错误，不能假定文件没有变化。", path)], resultUnknown: true);
        }

        AuthoringDocumentFile saved;
        try
        {
            saved = ReadDocument(path);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException or FormatException or JsonException)
        {
            throw UnknownAdvancedSaveFailure(path, "写入后读取档案失败。", error.Message);
        }

        EnsureAdvancedSaveReadback(path, saved, writtenContent, expectedContentHash, expectedRevision + 1, writtenDocument);
        return new AdvancedSaveResult(saved, expectedContentHash);
    }

    public AdvancedSaveCheckResult CheckAdvancedSave(string path, string content, string sourceHash, int expectedRevision)
    {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(content) || string.IsNullOrWhiteSpace(sourceHash) || expectedRevision < 1)
            throw new AuthoringSaveFailureException("WB-AUTHORING-SAVE-400", 400, "检查高级保存结果需要完整的档案版本信息。" );

        var candidate = _workspace.ValidateAuthoringContent(path, content);
        if (!candidate.Report.Valid) throw CandidateSaveFailure(candidate, path);
        var expectedDocument = JsonNode.Parse(candidate.Document.ToJsonString())!.AsObject();
        expectedDocument["revision"] = expectedRevision + 1;
        var expectedContent = FormatAuthoringDocument(expectedDocument, path);
        var expectedContentHash = Hashing.Sha256Text(expectedContent);

        AuthoringDocumentFile current;
        try
        {
            current = ReadDocument(path);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            throw new AuthoringSaveFailureException("WB-AUTHORING-UNKNOWN-503", 503, "暂时无法读取保存结果，请稍后再次检查。", [new Diagnostic("WB-AUTHORING-UNKNOWN-503", "warning", "读取磁盘版本失败，不能判断保存是否完成。", path)], resultUnknown: true);
        }

        var actualHash = current.Report.InputHash ?? Hashing.FileSha256(current.AbsolutePath);
        var actualRevision = IntegerValue(current.Document["revision"]) ?? 0;
        if (string.Equals(actualHash, sourceHash, StringComparison.OrdinalIgnoreCase) && actualRevision == expectedRevision)
            return new AdvancedSaveCheckResult("not-submitted", ExpectedContentHash: expectedContentHash);
        if (actualRevision > expectedRevision && SemanticDocumentEqual(current.Document, expectedDocument) && string.Equals(actualHash, expectedContentHash, StringComparison.OrdinalIgnoreCase))
        {
            var editor = ReadEditorDocument(path);
            return new AdvancedSaveCheckResult("confirmed", editor, current, expectedContentHash);
        }
        if (!string.Equals(actualHash, sourceHash, StringComparison.OrdinalIgnoreCase) || actualRevision != expectedRevision)
            return new AdvancedSaveCheckResult("conflict", ExpectedContentHash: expectedContentHash);
        return new AdvancedSaveCheckResult("pending", ExpectedContentHash: expectedContentHash);
    }

    public void EnsureDocumentVersion(string path, string sourceHash, int revision)
    {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(sourceHash) || revision < 1)
            throw new InvalidOperationException("WB-OP-VERSION-400: 操作缺少档案版本信息。" );
        var current = ReadDocument(path);
        var actualHash = current.Report.InputHash ?? Hashing.FileSha256(current.AbsolutePath);
        var actualRevision = IntegerValue(current.Document["revision"]) ?? 0;
        if (!string.Equals(actualHash, sourceHash, StringComparison.OrdinalIgnoreCase) || actualRevision != revision)
            throw new InvalidOperationException("WB-OP-VERSION-409: 当前档案已经变化，请重新读取后再执行操作。" );
    }

    internal static void EnsureAdvancedSaveReadback(string path, AuthoringDocumentFile saved, string expectedContent, string expectedContentHash, int expectedRevision, JsonObject expectedDocument)
    {
        var actualHash = saved.Report.InputHash ?? Hashing.Sha256Text(saved.Content);
        var actualRevision = IntegerValue(saved.Document["revision"]) ?? 0;
        if (string.Equals(saved.Content, expectedContent, StringComparison.Ordinal)
            && string.Equals(actualHash, expectedContentHash, StringComparison.OrdinalIgnoreCase)
            && actualRevision == expectedRevision
            && SemanticDocumentEqual(saved.Document, expectedDocument))
            return;

        var detail = $"期望 hash={expectedContentHash}、revision={expectedRevision}；实际 hash={actualHash}、revision={actualRevision}。";
        throw UnknownAdvancedSaveFailure(path, "写入后读取到的档案与预期不一致。", detail);
    }

    private static AuthoringSaveFailureException UnknownAdvancedSaveFailure(string path, string message, string detail)
        => new("WB-AUTHORING-UNKNOWN-503", 503, "文件写入结果暂时无法确认，请点击“检查保存结果”。", [new Diagnostic("WB-AUTHORING-UNKNOWN-503", "warning", message, path, detail)], resultUnknown: true);

    private static AuthoringSaveFailureException CandidateSaveFailure(AuthoringDocumentFile candidate, string path)
    {
        var parseFailure = candidate.Report.Diagnostics.Any(item => item.Code.StartsWith("WB-YAML-", StringComparison.Ordinal) || item.Code.StartsWith("WB-JSON-", StringComparison.Ordinal));
        var code = parseFailure ? "WB-AUTHORING-PARSE-422" : "WB-AUTHORING-SCHEMA-422";
        var message = parseFailure ? "高级文件无法解析，请检查 YAML 或 JSON 格式。" : "高级文件未通过世界书结构检查，请按诊断提示修正。";
        return new AuthoringSaveFailureException(code, 422, message, candidate.Report.Diagnostics.Count > 0 ? candidate.Report.Diagnostics : [new Diagnostic(code, "error", message, path)]);
    }

    private static bool SemanticDocumentEqual(JsonObject left, JsonObject right)
    {
        var leftCopy = JsonNode.Parse(left.ToJsonString())!.AsObject();
        var rightCopy = JsonNode.Parse(right.ToJsonString())!.AsObject();
        leftCopy.Remove("revision");
        rightCopy.Remove("revision");
        return string.Equals(CanonicalJson.Serialize(leftCopy), CanonicalJson.Serialize(rightCopy), StringComparison.Ordinal);
    }

    public AuthoringDocumentFile CreateGeneratedDocument(
        string title,
        string domain,
        string contentTier = "base",
        string authorId = "author.developer",
        string? subdomain = null,
        IReadOnlyList<string>? relatedDomains = null,
        string? eraKey = null,
        string? eraCertainty = null,
        IReadOnlyList<string>? entityIds = null)
    {
        ValidateDocumentMetadata(domain, contentTier, authorId, requireSubdomain: !string.IsNullOrWhiteSpace(subdomain), subdomain, relatedDomains);

        var normalizedTitle = string.IsNullOrWhiteSpace(title) ? "未命名世界书档案" : title.Trim();
        for (var attempt = 0; attempt < 8; attempt++)
        {
            var slug = $"entry-{Guid.NewGuid():N}";
            var path = $"authoring/{domain}/{slug}.yaml";
            if (File.Exists(Path.Combine(_workspace.Root, path))) continue;
            return CreateDocument(path, $"doc.{domain}.{slug}", normalizedTitle, domain, contentTier, authorId, subdomain, relatedDomains, eraKey, eraCertainty, entityIds);
        }

        throw new InvalidOperationException("WB-DOC-006: 无法为新档案生成唯一保存位置。");
    }

    internal AuthoringDocumentFile CreateGeneratedDocumentFromDraft(
        string title,
        string? summary,
        string domain,
        string draftId,
        IReadOnlyList<AuthoringDraftFact> facts,
        IReadOnlyList<AuthoringDraftExpression> expressions,
        string contentTier = "base",
        string authorId = "author.developer",
        AuthoringCandidateSet? candidateSet = null,
        string? subdomain = null,
        IReadOnlyList<string>? relatedDomains = null,
        AuthoringDraftMetadataReview? metadataReview = null,
        string? eraKey = null,
        string? eraCertainty = null,
        IReadOnlyList<string>? entityIds = null)
    {
        if (facts is null || facts.Count == 0)
            throw new InvalidOperationException("WB-AI-DRAFT-422: 没有可写入档案的客观事实。" );
        if (string.IsNullOrWhiteSpace(draftId))
            throw new InvalidOperationException("WB-AI-DRAFT-422: 草稿编号缺失，请重新开始。" );

        ValidateDocumentMetadata(domain, contentTier, authorId, requireSubdomain: !string.IsNullOrWhiteSpace(subdomain), subdomain, relatedDomains);
        var registryReport = new ValidationReport();
        var registries = _registries.LoadAndValidate(registryReport);
        if (!registryReport.Valid)
            throw new InvalidOperationException("WB-DOC-005: 身份目录当前不可用，请先刷新工作室。" );
        foreach (var profileId in expressions.SelectMany(item => item.ProfileIds).Distinct(StringComparer.Ordinal))
            if (!registries.Profiles.Contains(profileId))
                throw new InvalidOperationException($"WB-AI-DRAFT-422: 身份表达包含未登记身份：{profileId}。" );

        var normalizedTitle = string.IsNullOrWhiteSpace(title) ? "未命名世界书档案" : title.Trim();
        for (var attempt = 0; attempt < 8; attempt++)
        {
            var slug = $"entry-{Guid.NewGuid():N}";
            var path = $"authoring/{domain}/{slug}.yaml";
            if (File.Exists(Path.Combine(_workspace.Root, path))) continue;
            var template = AuthoringTemplateFactory.Create($"doc.{domain}.{slug}", normalizedTitle, domain, contentTier, authorId, slug, registries, subdomain, relatedDomains, eraKey, eraCertainty, entityIds);
            var candidate = AuthoringDraftDocumentBuilder.Build(template, draftId, normalizedTitle, summary, domain, facts, expressions, registries, candidateSet, metadataReview);
            var candidateReport = new ValidationReport();
            _schema.Validate(candidate, Path.Combine(_workspace.SchemaRoot, "awake.worldbook.authoring.v1.schema.json"), candidateReport);
            if (!candidateReport.Valid)
                throw new InvalidOperationException("WB-AI-DRAFT-422: AI 草稿写入前校验失败，请回到作者表单检查内容。" );

            SaveAuthoring(path, FormatAuthoringDocument(candidate, path));
            return ReadDocument(path);
        }

        throw new InvalidOperationException("WB-DOC-006: 无法为新档案生成唯一保存位置。" );
    }

    public AuthoringDocumentFile CreateDocument(
        string path,
        string documentId,
        string title,
        string domain,
        string contentTier = "base",
        string authorId = "author.developer",
        string? subdomain = null,
        IReadOnlyList<string>? relatedDomains = null,
        string? eraKey = null,
        string? eraCertainty = null,
        IReadOnlyList<string>? entityIds = null)
    {
        if (!Regex.IsMatch(documentId ?? string.Empty, "^doc\\.[a-z][a-z0-9_]*\\.[a-z0-9]+(?:[._-][a-z0-9]+)*$", RegexOptions.CultureInvariant))
            throw new InvalidOperationException("WB-DOC-001: 文档 ID 必须符合 doc.<domain>.<slug> 格式。");
        ValidateDocumentMetadata(domain, contentTier, authorId, requireSubdomain: false, subdomain, relatedDomains);

        var registryReport = new ValidationReport();
        var registries = _registries.LoadAndValidate(registryReport);
        if (!registryReport.Valid)
            throw new InvalidOperationException($"WB-DOC-005: registry 不可用。{registryReport.Diagnostics.FirstOrDefault()?.Message}");

        var normalizedTitle = string.IsNullOrWhiteSpace(title) ? "未命名世界书档案" : title.Trim();
        var normalizedDocumentId = documentId!;
        var normalizedAuthorId = authorId!;
        var slug = normalizedDocumentId[(normalizedDocumentId.LastIndexOf('.') + 1)..];
        var document = AuthoringTemplateFactory.Create(normalizedDocumentId, normalizedTitle, domain, contentTier, normalizedAuthorId, slug, registries, subdomain, relatedDomains, eraKey, eraCertainty, entityIds);
        var extension = Path.GetExtension(path).ToLowerInvariant();
        var content = extension == ".json"
            ? document.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine
            : SerializeYaml(document);
        SaveAuthoring(path, content);
        return ReadDocument(path);
    }

    private void ValidateDocumentMetadata(string domain, string contentTier, string authorId, bool requireSubdomain, string? subdomain, IReadOnlyList<string>? relatedDomains)
    {
        var taxonomy = _workspace.Taxonomy.Load();
        if (!taxonomy.TryGetDomain(domain, out _))
            throw new InvalidOperationException("WB-DOC-002: 请选择一个有效的知识分类。");
        var document = new JsonObject { ["domain"] = domain };
        if (subdomain is not null) document["subdomain"] = subdomain;
        if (relatedDomains is not null) document["related_domains"] = new JsonArray(relatedDomains.Select(value => JsonValue.Create(value) as JsonNode).ToArray()!);
        var taxonomyReport = new ValidationReport();
        taxonomy.ValidateDocument(document, taxonomyReport, requireSubdomain);
        if (!taxonomyReport.Valid) throw new InvalidOperationException($"WB-TAXONOMY-422: {taxonomyReport.Diagnostics.First(x => x.Severity == "error").Message}");
        if (contentTier is not ("base" or "adult_optional"))
            throw new InvalidOperationException("WB-DOC-003: content tier 无效。");
        if (!Regex.IsMatch(authorId ?? string.Empty, "^author\\.[a-z0-9]+(?:[._-][a-z0-9]+)*$", RegexOptions.CultureInvariant))
            throw new InvalidOperationException("WB-DOC-004: author_id 格式无效。");
    }

    public JsonObject GetEditorCatalog()
    {
        var taxonomy = _workspace.Taxonomy.Load();
        var report = new ValidationReport();
        var registries = _registries.LoadAndValidate(report);
        return AuthoringEditorProjection.BuildCatalog(registries, report, _entities.LoadAndValidate(), taxonomy);
    }

    private static string SerializeYaml(JsonObject document)
    {
        var serializer = new SerializerBuilder().Build();
        return serializer.Serialize(ToYamlValue(document));
    }

    private static int? IntegerValue(JsonNode? node)
    {
        if (node is null) return null;
        try { return node.GetValue<int>(); }
        catch (InvalidOperationException) { return checked((int)node.GetValue<long>()); }
    }

    private static void EnsureRegistryBinding(JsonObject binding, RegistrySnapshot registries)
    {
        var profileVersion = BindingValue(binding, "profileVersion", "profile_registry_version");
        var profileHash = BindingValue(binding, "profileHash", "profile_registry_hash");
        var referralVersion = BindingValue(binding, "referralVersion", "referral_registry_version");
        var referralHash = BindingValue(binding, "referralHash", "referral_registry_hash");
        if (!string.Equals(profileVersion, registries.ProfileVersion, StringComparison.Ordinal)
            || !string.Equals(profileHash, registries.ProfileHash, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(referralVersion, registries.ReferralVersion, StringComparison.Ordinal)
            || !string.Equals(referralHash, registries.ReferralHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("WB-REGISTRY-CAS-409: 身份或推荐对象注册表已更新，请刷新作者模式后再保存。");
    }

    private static string? BindingValue(JsonObject binding, string camelKey, string snakeKey)
        => Text(binding[camelKey]) ?? Text(binding[snakeKey]);

    private static string? Text(JsonNode? node) => node?.GetValue<string>();

    private static object? ToYamlValue(JsonNode? node)
    {
        if (node is JsonObject obj)
            return obj.ToDictionary(x => x.Key, x => ToYamlValue(x.Value), StringComparer.Ordinal);
        if (node is JsonArray array)
            return array.Select(ToYamlValue).ToList();
        if (node is JsonValue value)
        {
            if (value.TryGetValue<bool>(out var boolean)) return boolean;
            if (value.TryGetValue<int>(out var smallInteger)) return smallInteger;
            if (value.TryGetValue<long>(out var integer)) return integer;
            if (value.TryGetValue<decimal>(out var decimalValue)) return decimalValue;
            if (value.TryGetValue<string>(out var text)) return text;
        }
        return null;
    }
    internal string WriteCompiled(CompileResult compiled, string? outputRoot = null, Action? afterTempCandidate = null, Action? afterTargetReplace = null, Action<string, string>? beforeTargetReplace = null)
    {
        if (compiled is null || !compiled.Validation.Valid || compiled.Manifest is null) throw new InvalidOperationException("WB-COMPILE-001: 校验失败，不能写入 compiled。");
        CompiledPackageGuard.Ensure(compiled, "WB-COMPILE");
        var targetRoot = _workspace.Policy.RequireCompiled(outputRoot ?? Path.Combine(_workspace.Root, "compiled"), "compiled 输出");
        var parentRoot = Directory.GetParent(targetRoot)?.FullName ?? throw new InvalidOperationException("WB-COMPILE-002: compiled 输出目录无效。");
        var targetName = Path.GetFileName(targetRoot);
        var transactionPrefix = $".{targetName}.";
        var stagingRoot = _workspace.Policy.RequireCompiledTransaction(Path.Combine(parentRoot, $"{transactionPrefix}{Guid.NewGuid():N}.tmp"), parentRoot, transactionPrefix, "compiled staging");
        var backupRoot = _workspace.Policy.RequireCompiledTransaction(Path.Combine(parentRoot, $"{transactionPrefix}{Guid.NewGuid():N}.previous"), parentRoot, transactionPrefix, "compiled backup");
        Directory.CreateDirectory(stagingRoot);
        var targetReplaced = false;
        try
        {
            foreach (var item in compiled.Files)
            {
                var target = _workspace.Policy.RequireCompiledTransactionFile(Path.Combine(stagingRoot, item.Key), stagingRoot, "compiled 文件写入");
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.WriteAllBytes(target, item.Value);
            }
            var manifestPath = _workspace.Policy.RequireCompiledTransactionFile(Path.Combine(stagingRoot, "manifest.json"), stagingRoot, "compiled 清单写入");
            File.WriteAllText(manifestPath, CanonicalJson.Serialize(compiled.Manifest), new UTF8Encoding(false));
            var checksums = Directory.EnumerateFiles(stagingRoot, "*", SearchOption.AllDirectories)
                .Where(x => !x.EndsWith("SHA256SUMS.txt", StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => Path.GetRelativePath(stagingRoot, x).Replace('\\', '/'), StringComparer.Ordinal)
                .Select(x => $"{Hashing.FileSha256(x)}  {Path.GetRelativePath(stagingRoot, x).Replace('\\', '/')}");
            var checksumsPath = _workspace.Policy.RequireCompiledTransactionFile(Path.Combine(stagingRoot, "SHA256SUMS.txt"), stagingRoot, "compiled 哈希写入");
            File.WriteAllText(checksumsPath, string.Join(Environment.NewLine, checksums) + Environment.NewLine, Encoding.ASCII);
            afterTempCandidate?.Invoke();

            beforeTargetReplace?.Invoke(stagingRoot, backupRoot);
            if (Directory.Exists(targetRoot)) Directory.Move(targetRoot, backupRoot);
            Directory.Move(stagingRoot, targetRoot);
            targetReplaced = true;
            afterTargetReplace?.Invoke();
            return targetRoot;
        }
        catch
        {
            if (Directory.Exists(stagingRoot))
            {
                try { Directory.Delete(stagingRoot, true); } catch { }
            }
            if (!targetReplaced && !Directory.Exists(targetRoot) && Directory.Exists(backupRoot))
            {
                try { Directory.Move(backupRoot, targetRoot); } catch { }
            }
            throw;
        }
    }

    internal string Export(string contentTier = "base", string? confirmationToken = null, string? outputRoot = null, PublishFaultPoint faultPoint = PublishFaultPoint.None)
    {
        var compiled = CompileCore(contentTier, confirmationToken);
        if (!compiled.Validation.Valid) throw new InvalidOperationException($"WB-EXPORT-001: {compiled.Validation.Diagnostics.First(x => x.Severity == "error").Code}");
        return new AtomicCandidatePublisher(_workspace).Publish(compiled, contentTier, confirmationToken, outputRoot, faultPoint);
    }
    internal CompileResult Compile(string contentTier = "base", string? confirmationToken = null)
        => CompileCore(contentTier, confirmationToken);

    internal CompileResult CompileExact(IReadOnlyList<string> authoringPaths, string contentTier = "base", string? confirmationToken = null)
    {
        if (authoringPaths is null || authoringPaths.Count == 0) throw new InvalidOperationException("WB-COMPILE-PROOF-400: CompileProof 未提供输入集合。");
        return CompileCore(contentTier, confirmationToken, SnapshotInputStore.Capture(_workspace, authoringPaths));
    }

    private CompileResult CompileCore(string contentTier = "base", string? confirmationToken = null, SnapshotInputStore? inputStore = null)
    {
        var snapshot = inputStore is null ? BuildSnapshot() : BuildSnapshot(inputStore);
        if (contentTier is not ("base" or "adult_optional"))
        {
            snapshot.Report.Error("WB-TIER-002", "content tier 无效。", contentTier);
        }
        else if (contentTier == "base" && snapshot.HasAdultClosure)
        {
            snapshot.Report.Error("WB-TIER-001", "base 导出闭包包含 adult_optional 或未知分层内容。", "content-graph");
        }
        else if (contentTier == "adult_optional" && !string.Equals(confirmationToken, ConfirmationToken(snapshot, contentTier), StringComparison.Ordinal))
        {
            snapshot.Report.Error("WB-CONFIRM-001", "adult_optional confirmation token 缺失、错误或已过期。", "confirmation_token");
        }
        FinalizeReport(snapshot.Report);

        var result = new CompileResult(snapshot.Report) { Snapshot = snapshot, ConfirmationToken = ConfirmationToken(snapshot, contentTier) };
        if (!snapshot.Report.Valid) return result;

        var documents = snapshot.Documents.OrderBy(x => x.Document["id"]?.GetValue<string>(), StringComparer.Ordinal).ToList();
        var docs = new JsonArray(documents.Select(x => CanonicalJson.Canonicalize(x.Document)).ToArray());
        var contentHash = CanonicalJson.Hash(docs);
        var index = new JsonObject
        {
            ["format"] = "awake.worldbook.v2",
            ["documents"] = new JsonArray(documents.Select(x => x.Document["id"]?.GetValue<string>()).Where(x => x is not null).Select(x => JsonValue.Create(x)).ToArray()),
            ["content_hash"] = contentHash
        };
        var mapping = new JsonObject
        {
            ["format"] = "awake.runtime_mapping_report.v2",
            ["status"] = "ready_for_v2",
            ["target"] = "AWAKE awake.worldbook.v2 loader",
            ["runtime_file"] = "runtime.json",
            ["index_file"] = "index.json",
            ["notes"] = "编译结果由新 v2 读取器消费；不生成 v1 兼容 worldbook 文件。"
        };
        var validationJson = JsonSerializer.SerializeToNode(snapshot.Report) ?? new JsonObject();
        var sourceReport = new JsonObject { ["format"] = "awake.worldbook.source-report.v1", ["registry_count"] = snapshot.SourceRegistry.Count, ["registry_hash"] = snapshot.SourceRegistryHash };
        var auditReport = new JsonObject { ["format"] = "awake.worldbook.audit-report.v1", ["event_count"] = snapshot.AuditEvents.Count, ["ledger_files"] = snapshot.LedgerFileCount };
        var idReport = new JsonObject { ["format"] = "awake.worldbook.id-report.v1", ["status"] = "validated" };

        result.Files["documents.json"] = EncodingForJson(docs);
        result.Files["index.json"] = EncodingForJson(index);
        result.Files["content-graph.json"] = EncodingForJson(snapshot.ContentGraph);
        result.Files["runtime_mapping_report.json"] = EncodingForJson(mapping);
        result.Files["validation.json"] = EncodingForJson(validationJson);
        result.Files["source-report.json"] = EncodingForJson(sourceReport);
        result.Files["audit-report.json"] = EncodingForJson(auditReport);
        result.Files["id-report.json"] = EncodingForJson(idReport);
        var runtimePackage = RuntimePackageCompiler.Build(snapshot, documents, contentTier);
        result.Files["runtime.json"] = EncodingForJson(runtimePackage.Runtime);
        result.Files["index.json"] = EncodingForJson(runtimePackage.Index);
        result.Files["package-manifest.json"] = EncodingForJson(runtimePackage.Manifest);
        result.Manifest = runtimePackage.Manifest;
        result.ManifestHash = runtimePackage.ManifestHash;
        snapshot.Report.ManifestHash = result.ManifestHash;
        result.Files["validation.json"] = EncodingForJson(JsonSerializer.SerializeToNode(snapshot.Report)!);
        return result;
    }

    public PreviewResult Preview(string profileId, string fixtureId = "preview.default", IdentitySnapshot? identity = null)
    {
        var snapshot = BuildSnapshot();
        var actualIdentity = identity ?? new IdentitySnapshot($"snapshot.{fixtureId}", profileId, null, null, null, null, null, null);
        var result = new PreviewResult { FixtureId = fixtureId, ProfileId = profileId };
        if (!snapshot.Registries.Profiles.Contains(profileId))
        {
            snapshot.Report.Error("WB-PROFILE-001", "预览 profile 不存在。", profileId);
            result.Diagnostics = snapshot.Report.Diagnostics;
            result.Envelope = BuildPreviewEnvelope(snapshot, result, actualIdentity, new JsonArray(), new JsonArray());
            ValidatePreviewEnvelope(result.Envelope, snapshot.Report);
            snapshot.VerifyForOutput();
            return result;
        }

        var projection = PreviewProjectionBuilder.Build(snapshot.Documents, snapshot.Registries, profileId, actualIdentity);
        var npcResults = projection.NpcResults;
        var diagnosticResults = projection.DiagnosticResults;

        result.Envelope = BuildPreviewEnvelope(snapshot, result, actualIdentity, npcResults, diagnosticResults);
        ValidatePreviewEnvelope(result.Envelope, snapshot.Report);
        result.Items = result.Envelope["npc_preview"]?["results"]?.AsArray().Select(x => ToPreviewItem(x!, snapshot)).ToList() ?? [];
        result.Diagnostics = snapshot.Report.Diagnostics;
        if (result.Items.Count == 0) result.Diagnostics.Add(new Diagnostic("WB-PREVIEW-001", "info", "当前身份没有可见档案。"));
        snapshot.VerifyForOutput();
        return result;
    }

    private ValidatedSnapshot BuildSnapshot()
    {
        _workspace.Initialize();
        ValidatedSnapshot? previousAttempt = null;
        for (var attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                var inputs = SnapshotInputStore.Capture(_workspace);
                var snapshot = BuildSnapshot(inputs);
                if (previousAttempt is not null && HasRevisionRollback(previousAttempt, snapshot))
                    throw new InvalidOperationException("WB-CAS-409: 世界书输入 revision 在快照重建期间回退。");
                if (inputs.VerifyCurrent(WorkspaceReadStage.AssemblyVerification))
                {
                    snapshot.ReadInventory = inputs.ReadInventory;
                    snapshot.DownstreamReadCount = inputs.DownstreamReadCount;
                    return snapshot;
                }
                previousAttempt = snapshot;
            }
            catch (SnapshotInputChangedException) when (attempt == 0)
            {
            }
        }

        throw new InvalidOperationException("WB-CAS-409: 世界书输入在快照建立期间持续变化，未生成混合验证结果。");
    }

    private static bool HasRevisionRollback(ValidatedSnapshot previous, ValidatedSnapshot current)
    {
        var previousRevisions = EnumerateRevisions(previous.Documents).ToDictionary(x => x.Id, x => x.Revision, StringComparer.Ordinal);
        return EnumerateRevisions(current.Documents).Any(item => previousRevisions.TryGetValue(item.Id, out var previousRevision) && item.Revision < previousRevision);

        static IEnumerable<(string Id, int Revision)> EnumerateRevisions(IReadOnlyList<(string Path, JsonObject Document, ValidationReport Report)> documents)
        {
            foreach (var item in documents)
            {
                foreach (var nested in Enumerate(item.Document)) yield return nested;
            }

            static IEnumerable<(string Id, int Revision)> Enumerate(JsonObject value)
            {
                var id = value["id"]?.GetValue<string>();
                var revision = IntegerValue(value["revision"]);
                if (!string.IsNullOrWhiteSpace(id) && revision.HasValue) yield return (id, revision.Value);
                foreach (var assertion in value["assertions"]?.AsArray().OfType<JsonObject>() ?? [])
                {
                    foreach (var nested in Enumerate(assertion)) yield return nested;
                    foreach (var expression in assertion["expressions"]?.AsArray().OfType<JsonObject>() ?? [])
                        foreach (var nested in Enumerate(expression)) yield return nested;
                }
            }
        }
    }

    private ValidatedSnapshot BuildSnapshot(SnapshotInputStore inputs)
    {
        var report = new ValidationReport();
        var documents = _workspace.LoadDocuments(inputs);
        report.InputHash = Hashing.Sha256Text(string.Join("|", documents.Select(x => x.Report.InputHash).Where(x => x is not null).OrderBy(x => x, StringComparer.Ordinal)));

        if (documents.Count == 0) report.Warning("WB-WORKSPACE-001", "工作区没有 authoring/*.yaml 正典档案。");
        foreach (var item in documents) report.Diagnostics.AddRange(item.Report.Diagnostics);

        var sourceRegistry = _sources.LoadAndValidate(report, inputs);
        _sources.ValidateDocumentSources(documents, sourceRegistry, report, inputs);
        var auditEvents = _audit.LoadAndValidate(documents, report, inputs);
        _audit.ValidateLedger(report, inputs);
        var registries = _registries.LoadAndValidate(report, inputs);
        ValidateRegistryBindings(documents, registries, report);
        ValidateAuthorityAndCanon(documents, report);
        ValidateRedirects(documents, report);
        ValidateTimeline(documents, sourceRegistry, report);
        var graph = ContentGraphBuilder.Build(documents, sourceRegistry, registries, report, out var hasAdultClosure);
        _schema.Validate(graph, Path.Combine(_workspace.SchemaRoot, "content-graph.v1.schema.json"), report, inputs);
        ValidatePermissionReferences(documents, registries, report);
        FinalizeReport(report);
        return new ValidatedSnapshot
        {
            Documents = documents,
            Report = report,
            Registries = registries,
            SourceRegistry = sourceRegistry,
            SourceRegistryHash = Hashing.Sha256Text(string.Join("|", sourceRegistry.OrderBy(x => x.Key.Id, StringComparer.Ordinal).ThenBy(x => x.Key.Version).Select(x => $"{x.Key.Id}@{x.Key.Version}:{CanonicalJson.Hash(x.Value)}"))),
            AuditEvents = auditEvents,
            LedgerFileCount = inputs.Files.Count(x => x.Category == "ledger"),
            ContentGraph = graph,
            HasAdultClosure = hasAdultClosure,
            InputStore = inputs,
            InputClosure = inputs.Files,
            InputFingerprint = inputs.Fingerprint,
            ReadInventory = inputs.ReadInventory,
            DownstreamReadCount = inputs.DownstreamReadCount
        };
    }

    private static void FinalizeReport(ValidationReport report)
    {
        report.ReportHash = null;
        report.ReportHash = Hashing.Sha256Text(CanonicalJson.Serialize(JsonSerializer.SerializeToNode(report)!));
    }

    private static string ConfirmationToken(ValidatedSnapshot snapshot, string tier)
        => Hashing.Sha256Text($"{snapshot.Report.InputHash}|{snapshot.SourceRegistryHash}|{snapshot.Registries.ProfileHash}|{snapshot.Registries.ReferralHash}|{tier}|{snapshot.Report.ReportHash}");

    private static void ValidateRegistryBindings(IReadOnlyList<(string Path, JsonObject Document, ValidationReport Report)> documents, RegistrySnapshot registries, ValidationReport report)
    {
        foreach (var item in documents)
        {
            var bindings = item.Document["registry_bindings"]?.AsObject();
            if (bindings is null) continue;
            if (bindings["profile_registry_version"]?.GetValue<string>() != registries.ProfileVersion || !string.Equals(bindings["profile_registry_hash"]?.GetValue<string>(), registries.ProfileHash, StringComparison.OrdinalIgnoreCase))
                report.Error("WB-REGISTRY-001", "profile registry version/hash 与当前登记不一致。", item.Path);
            if (bindings["referral_registry_version"]?.GetValue<string>() != registries.ReferralVersion || !string.Equals(bindings["referral_registry_hash"]?.GetValue<string>(), registries.ReferralHash, StringComparison.OrdinalIgnoreCase))
                report.Error("WB-REGISTRY-001", "referral registry version/hash 与当前登记不一致。", item.Path);
        }
    }

    private static void ValidateAuthorityAndCanon(IReadOnlyList<(string Path, JsonObject Document, ValidationReport Report)> documents, ValidationReport report)
    {
        foreach (var item in documents)
        {
            if (item.Document["status"]?.GetValue<string>() == "canon" && item.Document["author_created"] is JsonObject created && created["review_status"]?.GetValue<string>() != "approved")
                report.Error("WB-CANON-001", "canon 原创档案必须已批准。", item.Path);
            foreach (var assertion in item.Document["assertions"]?.AsArray().OfType<JsonObject>() ?? [])
            {
                if (item.Document["status"]?.GetValue<string>() == "canon" && assertion["author_created"] is JsonObject assertionCreated && assertionCreated["review_status"]?.GetValue<string>() != "approved")
                    report.Error("WB-CANON-001", "canon 原创 assertion 必须已批准。", assertion["id"]?.GetValue<string>());
                foreach (var expression in assertion["expressions"]?.AsArray().OfType<JsonObject>() ?? [])
                    if (item.Document["status"]?.GetValue<string>() == "canon" && expression["author_created"] is JsonObject expressionCreated && expressionCreated["review_status"]?.GetValue<string>() != "approved")
                        report.Error("WB-CANON-001", "canon 原创 expression 必须已批准。", expression["id"]?.GetValue<string>());
            }
        }
    }

    private static void ValidateRedirects(IReadOnlyList<(string Path, JsonObject Document, ValidationReport Report)> documents, ValidationReport report)
    {
        var redirects = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var item in documents)
            foreach (var redirect in item.Document["redirects"]?.AsArray().OfType<JsonObject>() ?? [])
            {
                var from = redirect["from"]?.GetValue<string>();
                var to = redirect["to"]?.GetValue<string>();
                var redirectId = redirect["redirect_id"]?.GetValue<string>();
                if (from is null || to is null || redirectId is null) continue;
                if (!redirects.TryAdd(from, to)) report.Error("WB-ID-001", "同一 ID 存在多个 redirect 目标。", from);
                if (from.Equals(to, StringComparison.Ordinal)) report.Error("WB-ID-001", "redirect 不能指向自身。", redirectId);
            }
        foreach (var start in redirects.Keys)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var current = start;
            while (redirects.TryGetValue(current, out var next))
            {
                if (!seen.Add(current))
                {
                    report.Error("WB-ID-001", "redirect 迁移存在循环。", start, string.Join(" -> ", seen));
                    break;
                }
                current = next;
            }
        }
    }
    private static void ValidateTimeline(IReadOnlyList<(string Path, JsonObject Document, ValidationReport Report)> documents, Dictionary<(string Id, string Version), JsonObject> sourceRegistry, ValidationReport report)
    {
        foreach (var item in documents)
        {
            var universe = item.Document["universe"]?.GetValue<string>();
            foreach (var sourceRef in item.Document["sources"]?.AsArray().OfType<JsonObject>() ?? [])
            {
                if (!sourceRegistry.TryGetValue((sourceRef["source_id"]?.GetValue<string>() ?? "", sourceRef["source_version"]?.GetValue<string>() ?? ""), out var source)) continue;
                if (universe == "warband_future" && source["universe"]?.GetValue<string>() == "awake_current") report.Error("WB-TIME-001", "future 档案引用当前宇宙来源但未声明迁移。", item.Path);
            }
            if (item.Document["universe"]?.GetValue<string>() == "unknown") report.Warning("WB-TIME-001", "档案宇宙未确定。", item.Path);
        }
    }

    private static void ValidatePermissionReferences(IReadOnlyList<(string Path, JsonObject Document, ValidationReport Report)> documents, RegistrySnapshot registries, ValidationReport report)
    {
        foreach (var item in documents)
        {
            foreach (var assertion in item.Document["assertions"]?.AsArray().OfType<JsonObject>() ?? [])
            foreach (var expression in assertion["expressions"]?.AsArray().OfType<JsonObject>() ?? [])
            {
                foreach (var rule in (expression["grants"]?.AsArray() ?? []).Concat(expression["denies"]?.AsArray() ?? []).OfType<JsonObject>())
                {
                    var profile = rule["profile_id"]?.GetValue<string>();
                    if (profile is null || !registries.Profiles.Contains(profile)) report.Error("WB-PROFILE-001", "权限规则引用未知 profile。", expression["id"]?.GetValue<string>(), profile);
                }
                foreach (var referral in expression["fallback_referral_ids"]?.AsArray().Select(x => x?.GetValue<string>()).Where(x => x is not null) ?? [])
                {
                    if (!registries.Referrals.Contains(referral!)) report.Error("WB-REFERRAL-001", "fallback referral 不存在。", expression["id"]?.GetValue<string>(), referral);
                    else if (!registries.PubliclyAskableReferrals.Contains(referral!)) report.Error("WB-REFERRAL-002", "fallback referral 目标未开放公开询问。", expression["id"]?.GetValue<string>(), referral);
                }
            }
        }
    }

    private void ValidatePreviewEnvelope(JsonObject envelope, ValidationReport report)
    {
        _schema.Validate(envelope, Path.Combine(_workspace.SchemaRoot, "preview-fixture.v1.schema.json"), report);
        var fixtureId = envelope["fixture_id"]?.GetValue<string>();
        var profileId = envelope["profile_id"]?.GetValue<string>();
        var npc = envelope["npc_preview"]?.AsObject();
        var diagnostics = envelope["author_diagnostics"]?.AsObject();
        if (npc?["fixture_id"]?.GetValue<string>() != fixtureId || npc?["profile_id"]?.GetValue<string>() != profileId || diagnostics?["fixture_id"]?.GetValue<string>() != fixtureId || diagnostics?["profile_id"]?.GetValue<string>() != profileId || diagnostics?["identity_snapshot"]?["profile_id"]?.GetValue<string>() != profileId)
            report.Error("WB-PREVIEW-001", "preview envelope 的 fixture/profile/identity 不一致。", "preview");
    }

    private static JsonObject BuildPreviewEnvelope(ValidatedSnapshot snapshot, PreviewResult result, IdentitySnapshot identity, JsonArray npcResults, JsonNode diagnostics)
    {
        var npc = new JsonObject { ["fixture_id"] = result.FixtureId, ["profile_id"] = result.ProfileId, ["results"] = npcResults };
        var diag = new JsonObject
        {
            ["fixture_id"] = result.FixtureId,
            ["profile_id"] = result.ProfileId,
            ["compile_version"] = snapshot.Report.ReportHash ?? "unknown",
            ["identity_snapshot"] = new JsonObject
            {
                ["snapshot_id"] = identity.SnapshotId,
                ["profile_id"] = identity.ProfileId,
                ["culture_id"] = identity.CultureId,
                ["kingdom_id"] = identity.KingdomId,
                ["settlement_id"] = identity.SettlementId,
                ["age"] = identity.Age,
                ["steward"] = identity.Steward
            },
            ["results"] = diagnostics
        };
        return new JsonObject
        {
            ["fixture_id"] = result.FixtureId,
            ["profile_id"] = result.ProfileId,
            ["profile_registry"] = new JsonObject { ["version"] = snapshot.Registries.ProfileVersion, ["hash"] = snapshot.Registries.ProfileHash },
            ["referral_registry"] = new JsonObject { ["version"] = snapshot.Registries.ReferralVersion, ["hash"] = snapshot.Registries.ReferralHash },
            ["npc_preview"] = npc,
            ["author_diagnostics"] = diag
        };
    }

    private static PreviewItem ToPreviewItem(JsonNode node, ValidatedSnapshot snapshot)
    {
        var result = node.AsObject();
        var referrals = result["fallback_referral_ids"]?.AsArray().Select(x => x?.GetValue<string>()).Where(x => x is not null).Select(x => x!).ToArray();
        var text = result["text"] is JsonObject localized ? Localized(localized) : result["status"]?.GetValue<string>();
        var itemId = result["knowledge_id"]?.GetValue<string>() ?? "unknown";
        return new PreviewItem(itemId, itemId, result["layer"]?.GetValue<string>() ?? "unknown", result["status"]?.GetValue<string>() ?? "unknown", text, referrals);
    }

    private static string? Localized(JsonObject localized) => localized["zh-CN"]?.GetValue<string>() ?? localized["zh"]?.GetValue<string>() ?? localized["en"]?.GetValue<string>() ?? localized.FirstOrDefault().Value?.GetValue<string>();
    private static byte[] EncodingForJson(JsonNode node) => Encoding.UTF8.GetBytes(CanonicalJson.Serialize(node));
}

public sealed class ValidatedSnapshot
{
    public required IReadOnlyList<(string Path, JsonObject Document, ValidationReport Report)> Documents { get; init; }
    public required ValidationReport Report { get; init; }
    public required RegistrySnapshot Registries { get; init; }
    public required Dictionary<(string Id, string Version), JsonObject> SourceRegistry { get; init; }
    public required string SourceRegistryHash { get; init; }
    public required Dictionary<string, JsonObject> AuditEvents { get; init; }
    public required int LedgerFileCount { get; init; }
    public required JsonObject ContentGraph { get; init; }
    public required bool HasAdultClosure { get; init; }
    public required SnapshotInputStore InputStore { get; init; }
    public required IReadOnlyList<SnapshotInputFile> InputClosure { get; init; }
    public required string InputFingerprint { get; init; }
    public IReadOnlyList<SnapshotReadInventoryEntry> ReadInventory { get; internal set; } = [];
    public int DownstreamReadCount { get; internal set; }

    public void VerifyForOutput()
    {
        if (!InputStore.VerifyCurrent(WorkspaceReadStage.OutputBoundary))
            throw new InvalidOperationException("WB-CAS-409: 世界书输入在输出边界发生变化，未发布混合结果。");
        ReadInventory = InputStore.ReadInventory;
        DownstreamReadCount = InputStore.DownstreamReadCount;
    }
}

public sealed class CompileResult
{
    public CompileResult(ValidationReport validation) => Validation = validation;
    public ValidationReport Validation { get; }
    public Dictionary<string, byte[]> Files { get; } = new(StringComparer.Ordinal);
    public JsonObject? Manifest { get; set; }
    public string? ManifestHash { get; set; }
    public string? ConfirmationToken { get; set; }
    public ValidatedSnapshot? Snapshot { get; set; }
}

public sealed class PreviewResult
{
    public string FixtureId { get; init; } = "preview.default";
    public string ProfileId { get; init; } = "profile.commoner";
    public List<PreviewItem> Items { get; set; } = [];
    public List<Diagnostic> Diagnostics { get; set; } = [];
    public JsonObject Envelope { get; set; } = new();
}

public sealed record AuthoringDocumentSummary(
    string Path,
    string Format,
    string? Id,
    string? Title,
    string? Domain,
    string? Status,
    string? ContentTier,
    int? Revision,
    bool Valid,
    IReadOnlyList<Diagnostic> Diagnostics);

public sealed record PreviewItem(string DocumentId, string? Title, string Layer, string Visibility, string? Text, string?[]? ReferralIds);
public sealed record IdentitySnapshot(string SnapshotId, string ProfileId, string? CultureId, string? KingdomId, string? SettlementId, int? Age, int? Steward, string? Scope);














