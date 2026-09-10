using System.Text.Json;
using System.Text.Json.Nodes;
using Awake.WorldbookStudio.Cli;
using Awake.WorldbookStudio.Core;

var command = args.FirstOrDefault()?.ToLowerInvariant() ?? "doctor";
var root = ReadOption(args, "--workspace") ?? Path.Combine(Environment.CurrentDirectory, "workspace");
var schemaRoot = ReadOption(args, "--schema-root") ?? FindSchemaRoot(Environment.CurrentDirectory);

try
{
    var workspace = new WorkspaceService(new WorkspaceOptions(root, schemaRoot));
    var service = new WorldbookApplicationService(workspace);
    var authority = new AuthorityGateService(workspace, service);
    var assistance = new AssistanceService(workspace);
    service.Initialize();
    authority.Initialize();
    if (command is "ai-apply" or "ai-reject") return PrintLegacyGone();
    if (command == "authoring-register")
    {
        var result = authority.RegisterDocument(RequiredOption(args, "--operation"), RequiredOption(args, "--path"));
        WriteJson(new JsonObject { ["ok"] = true, ["document_revision"] = AuthorityPublicProjection.Document(result, root) });
        return 0;
    }
    if (command == "authoring-select")
    {
        var ids = RequiredOption(args, "--document-id").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var result = authority.MaterializeSelection(RequiredOption(args, "--operation"), ids);
        WriteJson(new JsonObject { ["ok"] = true, ["selection"] = AuthorityPublicProjection.Selection(result) });
        return 0;
    }
    if (command == "authoring-approve")
    {
        var result = authority.ApproveSelection(RequiredOption(args, "--operation"), RequiredOption(args, "--selection"));
        WriteJson(new JsonObject { ["ok"] = true, ["approval_proof"] = AuthorityPublicProjection.Approval(result) });
        return 0;
    }
    if (command == "authoring-proof")
    {
        var result = authority.IssueCompileProof(RequiredOption(args, "--operation"), RequiredOption(args, "--approval"), ReadOption(args, "--content-tier") ?? "base");
        WriteJson(new JsonObject { ["ok"] = true, ["compile_proof"] = AuthorityPublicProjection.CompileProof(result) });
        return 0;
    }
    if (command == "publish-staging")
    {
        var path = authority.PublishStaging(RequiredOption(args, "--operation"), RequiredOption(args, "--staging"), RequiredOption(args, "--manifest-hash"), ReadOption(args, "--expected-current-manifest-hash"));
        WriteJson(new JsonObject { ["ok"] = true, ["pointer"] = AuthorityPublicProjection.Artifact("pointer", path, root)["pointer"] });
        return 0;
    }
    switch (command)
    {
        case "init":
            service.Initialize(seedSample: true);
            WriteJson(new JsonObject { ["ok"] = true, ["workspace"] = Path.GetFullPath(root) });
            return 0;
        case "validate":
            return PrintReport(service.Validate());
        case "compile":
        {
            var proof = ReadOption(args, "--proof");
            if (string.IsNullOrWhiteSpace(proof)) return PrintAuthorityFailure("WB-AUTHORITY-400: compile proof is required.", 400);
            var settlement = authority.CompileApproved(proof, ReadOption(args, "--confirm-adult"), ReadOption(args, "--out"));
            var envelope = settlement.PersistedEnvelope?.DeepClone()?.AsObject() ?? throw new InvalidOperationException("WB-AUTHORITY-OPERATION-409: compile settlement envelope 缺失。");
            WriteJson(CompileSettlementSupport.PublicProjection(envelope));
            return 0;
        }
        case "preview":
        {
            var profile = ReadOption(args, "--profile") ?? "profile.commoner";
            var fixture = ReadOption(args, "--fixture") ?? "preview.default";
            var preview = service.Preview(profile, fixture);
            WriteJson(preview.Envelope);
            return preview.Diagnostics.Any(x => x.Severity == "error") ? 2 : 0;
        }
        case "export":
        {
            var proof = ReadOption(args, "--proof");
            if (string.IsNullOrWhiteSpace(proof)) return PrintAuthorityFailure("WB-AUTHORITY-400: compile proof is required.", 400);
            var path = authority.ExportStaging(RequiredOption(args, "--operation"), proof, ReadOption(args, "--confirm-adult"), ReadOption(args, "--out"));
            WriteJson(new JsonObject { ["ok"] = true, ["staging"] = AuthorityPublicProjection.Artifact("staging", path, root)["staging"] });
            return 0;
        }
        case "ai-providers":
        {
            var settings = ProviderConfiguration.FromEnvironment();
            WriteObject(new { providers = new[] { settings.GetStatus("local"), settings.GetStatus("cloud") } });
            return 0;
        }
        case "ai-consent-preview":
        {
            var path = RequiredOption(args, "--path");
            var providerId = WorldbookInputNormalization.NormalizeProvider(ReadOption(args, "--provider"));
            var analysisKind = ParseAnalysis(ReadOption(args, "--analysis"));
            var settings = ProviderConfiguration.FromEnvironment();
            var status = settings.GetStatus(providerId);
            if (!string.Equals(status.State, "configured", StringComparison.Ordinal)) throw new InvalidOperationException($"WB-AI-PROVIDER-409: {status.Label}。");
            var request = assistance.CreateRequest(path, providerId, analysisKind);
            var bufferId = ReadOption(args, "--buffer-id") ?? "buffer-" + Guid.NewGuid().ToString("N");
            var consentStore = new CliAiConsentStore();
            var ticket = consentStore.Create(request, bufferId);
            WriteObject(new
            {
                ok = true,
                consent_token = ticket.Token,
                provider = status,
                analysis = AssistanceAnalysisNames.ToWire(analysisKind),
                scope = new
                {
                    document_id = request.DocumentId,
                    source_document_hash = request.SourceDocumentHash,
                    saved_revision = request.SavedRevision,
                    fields = request.DocumentProjection.Select(x => x.Key).OrderBy(x => x, StringComparer.Ordinal).ToArray(),
                    note = "只发送已保存当前档案的结构化投影与有限注册表摘要；不发送来源正文、其他档案或未保存编辑。"
                },
                buffer_id = bufferId,
                expires_at = ticket.Record.ExpiresAt
            });
            return 0;
        }
        case "ai-analyze":
        {
            var token = RequiredOption(args, "--consent");
            var path = RequiredOption(args, "--path");
            var consentStore = new CliAiConsentStore();
            var consent = consentStore.Consume(token, path);
            var analysisKind = ParseAnalysis(consent.Analysis);
            var request = assistance.CreateRequest(path, consent.ProviderId, analysisKind);
            if (!string.Equals(request.DocumentId, consent.DocumentId, StringComparison.Ordinal)
                || !string.Equals(request.RequestHash, consent.RequestHash, StringComparison.Ordinal)
                || !string.Equals(request.SourceDocumentHash, consent.SourceDocumentHash, StringComparison.Ordinal)
                || request.SavedRevision != consent.SavedRevision)
                throw new InvalidOperationException("WB-AI-CAS-409: 同意预览后档案已发生变化，请重新预览范围。");
            var provider = AssistanceProviderFactory.Create(request.ProviderId);
            var result = provider.AnalyzeAsync(request).GetAwaiter().GetResult();
            var saved = new SuggestionStore(workspace).Save(request, result, token, consent.BufferId);
            WriteObject(new
            {
                ok = true,
                request_hash = request.RequestHash,
                source_document_hash = request.SourceDocumentHash,
                saved_revision = request.SavedRevision,
                buffer_id = consent.BufferId,
                suggestions = saved.Select(ToSuggestionDto).ToArray()
            });
            return 0;
        }
        case "ai-apply":
        {
            var token = RequiredOption(args, "--consent");
            var path = RequiredOption(args, "--path");
            var bufferId = RequiredOption(args, "--buffer-id");
            var suggestionId = RequiredOption(args, "--suggestion");
            var applyNonce = RequiredOption(args, "--apply-nonce");
            var document = service.ReadDocument(path);
            var documentId = document.Document["id"]?.GetValue<string>() ?? throw new InvalidOperationException("WB-AI-CAS-409: 当前档案缺少稳定 ID。");
            var sourceHash = document.Report.InputHash ?? Hashing.FileSha256(document.AbsolutePath);
            var revision = WorldbookInputNormalization.ReadRevision(document.Document);
            var store = new SuggestionStore(workspace);
            var envelope = store.Read(suggestionId, documentId, sourceHash, revision, token, bufferId);
            var buffer = DocumentCas.Open(document.Path, documentId, sourceHash, revision, bufferId, document.Document);
            var updated = DocumentCas.Apply(buffer, envelope, applyNonce);
            store.ConsumeApplyNonce(suggestionId, documentId, sourceHash, revision, token, bufferId, applyNonce);
            WriteObject(new { ok = true, saved = false, buffer_id = updated.BufferId, revision = updated.Revision, content = service.FormatAuthoringDocument(updated.Document.AsObject(), document.Path), note = "建议只应用到编辑器缓冲区；尚未写入正典文件。" });
            return 0;
        }
        case "ai-reject":
        {
            var token = RequiredOption(args, "--consent");
            var path = RequiredOption(args, "--path");
            var bufferId = RequiredOption(args, "--buffer-id");
            var suggestionId = RequiredOption(args, "--suggestion");
            var applyNonce = RequiredOption(args, "--apply-nonce");
            var document = service.ReadDocument(path);
            var documentId = document.Document["id"]?.GetValue<string>() ?? throw new InvalidOperationException("WB-AI-CAS-409: 当前档案缺少稳定 ID。");
            var sourceHash = document.Report.InputHash ?? Hashing.FileSha256(document.AbsolutePath);
            var revision = WorldbookInputNormalization.ReadRevision(document.Document);
            new SuggestionStore(workspace).Reject(suggestionId, documentId, sourceHash, revision, token, bufferId, applyNonce);
            WriteJson(new JsonObject { ["ok"] = true, ["rejected"] = true });
            return 0;
        }
        case "doctor":
            WriteJson(new JsonObject { ["ok"] = true, ["sdk"] = "10.0.301", ["offline"] = true, ["schema_root"] = Path.GetFullPath(schemaRoot), ["workspace"] = Path.GetFullPath(root), ["v1_runtime_write"] = false, ["ai"] = true });
            return 0;
        default:
            Console.Error.WriteLine("用法: init|validate|authoring-register|authoring-select|authoring-approve|authoring-proof|compile|preview|export|publish-staging|ai-providers|ai-consent-preview|ai-analyze|ai-apply|ai-reject|doctor");
            return 3;
    }
}
catch (InvalidOperationException ex)
{
    var error = ex.Message.StartsWith("WB-AI-", StringComparison.Ordinal) ? ex.Message : ex.Message.Split(':', 2)[0];
    var status = error == "WB-AUTHORITY-MUTATION-UNKNOWN" ? 503 : 500;
    WriteJson(new JsonObject { ["ok"] = false, ["error"] = error, ["status"] = status, ["side_effect"] = ex is AuthorityMutationUnknownException ? "unknown" : "none" });
    return 3;
}
catch (Exception ex)
{
    // 意外异常过去只回一个无因由的 500，用户和日志都拿不到线索；把异常本身写到 stderr 便于排查。
    Console.Error.WriteLine($"[cli] {ex.GetType().Name}: {ex.Message}");
    WriteJson(new JsonObject { ["ok"] = false, ["error"] = "WB-AUTHORITY-UNKNOWN-500", ["side_effect"] = "none" });
    return 4;
}

static object ToSuggestionDto(SuggestionEnvelope envelope)
{
    var semantic = SuggestionSemanticProjection.Project(envelope);
    return new
    {
        id = semantic.Id,
        kind = semantic.Kind,
        severity = semantic.Severity,
        confidence = semantic.Confidence,
        title = semantic.Title,
        reason = semantic.Reason,
        candidate_text = semantic.CandidateText,
        patch = semantic.Patch,
        review_only = semantic.ReviewOnly,
        apply_nonce = semantic.ApplyNonce,
        suggestion_hash = semantic.SuggestionHash
    };
}

static AssistanceAnalysisKind ParseAnalysis(string? analysis)
    => analysis?.Trim().ToLowerInvariant() switch
    {
        "permissions" => AssistanceAnalysisKind.Permissions,
        "consistency" => AssistanceAnalysisKind.Consistency,
        "metadata" => AssistanceAnalysisKind.Metadata,
        "prose" => AssistanceAnalysisKind.Prose,
        _ => throw new InvalidOperationException("WB-AI-REQUEST-400: 分析类型无效。")
    };

static string RequiredOption(string[] values, string name)
    => ReadOption(values, name) ?? throw new InvalidOperationException($"WB-AI-REQUEST-400: 缺少 {name}。");

static int PrintReport(ValidationReport report)
{
    WriteJson(JsonSerializer.SerializeToNode(report)!);
    return report.Valid ? 0 : 2;
}

static int PrintLegacyGone()
{
    WriteJson(new JsonObject { ["ok"] = false, ["error"] = "WB-AUTHORITY-LEGACY-410", ["status"] = 410, ["side_effect"] = "none" });
    return 3;
}

static int PrintAuthorityFailure(string error, int status)
{
    WriteJson(new JsonObject { ["ok"] = false, ["error"] = error, ["status"] = status, ["side_effect"] = "none" });
    return 3;
}

static string? ReadOption(string[] values, string name)
{
    var index = Array.FindIndex(values, x => x.Equals(name, StringComparison.OrdinalIgnoreCase));
    return index >= 0 && index + 1 < values.Length ? values[index + 1] : null;
}

static string FindSchemaRoot(string start)
{
    var candidates = new[]
    {
        Path.Combine(AppContext.BaseDirectory, "schemas"),
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "schemas")),
        Path.Combine(start, "docs", "worldbook-studio-plan"),
        Path.GetFullPath(Path.Combine(start, "..", "..", "docs", "worldbook-studio-plan"))
    };
    foreach (var candidate in candidates)
    {
        var full = Path.GetFullPath(candidate);
        if (File.Exists(Path.Combine(full, "awake.worldbook.authoring.v1.schema.json"))) return full;
    }
    throw new DirectoryNotFoundException("WB-SCHEMA-404: 找不到 worldbook-studio 规范目录。");
}

static void WriteJson(JsonNode node) => Console.WriteLine(node.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
static void WriteObject(object value) => Console.WriteLine(JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
