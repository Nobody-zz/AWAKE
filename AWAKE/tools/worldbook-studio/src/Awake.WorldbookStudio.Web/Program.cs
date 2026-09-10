using System.Text.Json;
using System.Text.Json.Nodes;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Awake.WorldbookStudio.Core;
using Awake.WorldbookStudio.Web;

var builder = WebApplication.CreateBuilder(args);
var runtime = WebRuntimeBootstrap.Load();
var port = StudioRuntimeConstants.ResolvePort();
var workspaceRoot = runtime.WorkspaceRoot;
var schemaRoot = runtime.SchemaRoot;
var workspace = new WorkspaceService(new WorkspaceOptions(workspaceRoot, schemaRoot));
var service = new WorldbookApplicationService(workspace);
var assistance = new AssistanceService(workspace);
var suggestions = new SuggestionStore(workspace);
var sessions = new WebAiSessionStore($"http://127.0.0.1:{port}", workspace);
var drafts = new AuthoringDraftStore(workspace);
var handoffs = new WorkstationHandoffStore(workspace, drafts);
var handoffFaultInjection = WorkstationHandoffFaultInjection.ForEnvironment(builder.Environment);
var authority = new AuthorityGateService(workspace, service);
var batchContractPath = Path.Combine(runtime.PackageRoot, "schemas", "batch", "awake.worldbook.batch-authoring-contracts-r14.json");
var batchContract = File.Exists(batchContractPath) ? BatchAuthoringContractRegistry.Load(batchContractPath) : null;
var shutdownRequested = 0;
service.Initialize();
authority.Initialize();
// 启动期批量恢复改为后台执行，避免历史批次多的老工作区阻塞启动；批次端点前有门闩 await，对外时序不变。
var batchRecovery = Task.Run(() =>
{
    try { new BatchRecoveryService(workspace, batchContract).RecoverInFlight(); }
    catch (Exception error)
    {
        WebStartupLog.Write(new InvalidOperationException($"WB-BATCH-RECOVERY-UNKNOWN-503: 启动期批量恢复失败。{error.Message}", error));
        Console.Error.WriteLine($"WB-BATCH-RECOVERY-UNKNOWN-503: 启动期批量恢复失败。{error.Message}");
    }
});

builder.WebHost.UseUrls($"http://127.0.0.1:{port}");
var app = builder.Build();
const string AuthorityRegisterRoute = "/api/ai/authoring/documents/register";
const string AuthoritySelectionRoute = "/api/ai/authoring/selections";
const string AuthorityApprovalRoute = "/api/ai/authoring/selections/{selectionId}/approve";
const string AuthorityCompileProofRoute = "/api/ai/authoring/compile-proof";
const string AuthorityOperationsRoute = "/api/ai/authoring/operations/{operationId}";
const string AuthorityExportRoute = "/api/ai/authoring/export-staging";
const string AuthorityPublishRoute = "/api/ai/authoring/publish-staging";
const string CustomerCompileRoute = "/api/authoring/compile";
const string CustomerSaveRoute = "/api/authoring/save-authoring";
const string CustomerSaveCheckRoute = "/api/authoring/save-authoring/check";
const string CustomerEditorSaveRoute = "/api/authoring/save-editor-document";
const string CustomerCreateRoute = "/api/authoring/document/new";
const string CustomerExportRoute = "/api/authoring/export-staging";
const string CustomerAiApplyRoute = "/api/ai/authoring/apply";
const string CustomerAiRejectRoute = "/api/ai/authoring/reject";
const string AuthorityFailureContextItem = "awake.authority.failure-context";
const string BatchRoutePrefix = "/api/ai/authoring/batch";
const string LegacyBatchRoutePrefix = "/api/ai/batch";
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Exception ex)
    {
        var failureContext = context.Items.TryGetValue(AuthorityFailureContextItem, out var value) && value is AuthorityFailureContext selected
            ? selected
            : AuthorityFailureContext.Preflight;
        await AuthorityHttpErrorProjection.WriteAsync(context, ex, failureContext);
    }
});
app.Use(async (context, next) =>
{
    var path = context.Request.Path;
    if (path.StartsWithSegments(BatchRoutePrefix) || path.StartsWithSegments(LegacyBatchRoutePrefix))
        await batchRecovery.ConfigureAwait(false);
    await next();
});
app.Use(async (context, next) =>
{
    if (AuthorityGateRoutes.IsRetired(context.Request.Method, context.Request.Path.Value))
    {
        await AuthorityHttpErrorProjection.WriteAsync(context, new InvalidOperationException("WB-AUTHORITY-LEGACY-410"), AuthorityFailureContext.RetiredRoute);
        return;
    }
    await next();
});
app.UseDefaultFiles();
app.UseStaticFiles();
object Health()
{
    var workspaceHash = StudioRuntimeHashing.WorkspaceHash(runtime.WorkspaceRoot);
    return new
    {
        ok = true,
        workstation_id = "worldbook_studio",
        instance_id = runtime.Bootstrap.InstanceId,
        protocol_version = StudioRuntimeConstants.ProtocolVersion.ToString(),
        build_id = typeof(WorldbookApplicationService).Assembly.GetName().Version?.ToString() ?? "dev",
        workspace_id = "workspace." + workspaceHash,
        state = "ready",
        product = StudioRuntimeConstants.Product,
        protocolVersion = StudioRuntimeConstants.ProtocolVersion,
        instanceId = runtime.Bootstrap.InstanceId,
        launchNonceHash = StudioRuntimeHashing.Sha256Prefix(Convert.ToHexString(runtime.LaunchNonce).ToLowerInvariant()),
        workspaceIdHash = workspaceHash,
        workspaceHash,
        port
    };
}
object ProviderSettingsView()
{
    var configuration = ProviderConfiguration.FromEnvironment();
    var hasStored = ProviderSettingsStore.TryLoad(out var stored);
    var storedSettings = hasStored ? stored : null;
    var status = configuration.GetStatus("cloud");
    var hasStoredLocalWorker = ProviderSettingsStore.TryLoadLocalWorker(out _, out _);
    var localStatus = configuration.GetStatus("local");
    return new
    {
        baseUrl = storedSettings?.BaseUrl ?? configuration.CloudBaseUrl ?? string.Empty,
        model = storedSettings?.Model ?? configuration.CloudModel ?? string.Empty,
        hasApiKey = storedSettings is not null ? !string.IsNullOrWhiteSpace(storedSettings.ApiKey) : status.State == "configured",
        keyHint = storedSettings is not null ? ProviderSettingsStore.Hint(storedSettings.ApiKey) : status.State == "configured" ? "由环境变量提供" : string.Empty,
        source = hasStored ? "本机加密保存" : status.State == "configured" ? "环境变量" : "未配置",
        localWorkerUrl = configuration.LocalWorkerUrl ?? string.Empty,
        hasLocalWorkerSecret = configuration.ResolveLocalWorkerSecret() is { Length: > 0 },
        localWorkerConfigured = hasStoredLocalWorker
            ? true
            : localStatus.State == "configured",
        localWorkerSource = hasStoredLocalWorker ? "本机加密保存" : localStatus.State == "configured" ? "环境变量" : "未配置",
        localWorkerState = localStatus.State,
        localWorkerMessage = localStatus.Label
    };
}
app.MapGet("/health", () => Results.Ok(Health()));
app.MapGet("/api/health", () => Results.Ok(Health()));
app.MapPost("/api/launcher/shutdown", (HttpContext context, IHostApplicationLifetime lifetime) =>
{
    if (context.Connection.RemoteIpAddress is not { } remote || !IPAddress.IsLoopback(remote) || context.Request.Headers.ContainsKey("Origin"))
        return Results.Json(new { ok = false, error = "WB-SHUTDOWN-403" }, statusCode: StatusCodes.Status403Forbidden);

    var instanceId = context.Request.Headers["X-AWAKE-Instance-Id"].ToString();
    var proof = context.Request.Headers["X-AWAKE-Shutdown-Proof"].ToString();
    var expected = HMACSHA256.HashData(runtime.LaunchNonce, Encoding.UTF8.GetBytes("shutdown:v1:" + runtime.Bootstrap.InstanceId));
    byte[]? supplied = null;
    try { supplied = Convert.FromHexString(proof); } catch (FormatException) { }
    if (!string.Equals(instanceId, runtime.Bootstrap.InstanceId, StringComparison.Ordinal)
        || supplied is null
        || !CryptographicOperations.FixedTimeEquals(expected, supplied))
        return Results.Json(new { ok = false, error = "WB-SHUTDOWN-403" }, statusCode: StatusCodes.Status403Forbidden);

    if (Interlocked.Exchange(ref shutdownRequested, 1) == 0) lifetime.StopApplication();
    return Results.Ok(new { ok = true, shuttingDown = true });
});
app.MapGet("/api/workspace", () => Results.Ok(new { root = "本机 Studio 工作区", schemaRoot = "内置世界书规范", exportRoot = "workspace/export/WorldbookV2", offline = true }));
app.MapGet("/api/documents", () => Results.Ok(new { documents = service.ListDocuments() }));
app.MapGet("/api/document", (string? path) =>
{
    if (string.IsNullOrWhiteSpace(path)) return Results.BadRequest(new { ok = false, error = "WB-DOC-400: path 不能为空。" });
    try { return Results.Ok(service.ReadDocument(path)); }
    catch (InvalidOperationException ex) { return SafeFailure(ex); }
});
app.MapGet("/api/authoring/document", (string? path) =>
{
    if (string.IsNullOrWhiteSpace(path)) return Results.BadRequest(new { ok = false, error = "WB-DOC-400: path 不能为空。" });
    try
    {
        var document = service.ReadDocument(path);
        var workspaceHash = StudioRuntimeHashing.WorkspaceHash(runtime.WorkspaceRoot);
        return Results.Ok(new
        {
            ok = true,
            data = new
            {
                workspace_id = "workspace." + workspaceHash,
                document_id = document.Document["id"]?.GetValue<string>() ?? string.Empty,
                revision = WorldbookInputNormalization.ReadRevision(document.Document),
                content_sha256 = document.Report.InputHash ?? Hashing.FileSha256(document.AbsolutePath),
                path = document.Path
            }
        });
    }
    catch (InvalidOperationException ex) { return SafeFailure(ex); }
});
app.MapGet("/api/editor-document", (string? path) =>
{
    if (string.IsNullOrWhiteSpace(path)) return Results.BadRequest(new { ok = false, error = "WB-DOC-400: path 不能为空。" });
    try { return Results.Ok(service.ReadEditorDocument(path)); }
    catch (InvalidOperationException ex) { return EditorFailure(ex); }
});
app.MapGet("/api/editor-catalog", () =>
{
    try { return Results.Ok(service.GetEditorCatalog()); }
    catch (InvalidOperationException ex) { return EditorFailure(ex); }
});
app.MapPost("/api/validate", (string? path, string? sourceHash, int? revision) =>
{
    try
    {
        RequireDocumentVersion(service, path, sourceHash, revision);
        return Results.Json(service.Validate());
    }
    catch (InvalidOperationException ex) { return AiFailure(ex); }
});
app.MapPost("/api/compile", () => LegacyGone());
app.MapPost("/api/save-authoring", () => LegacyGone());
app.MapPost("/api/save-authoring/check", () => LegacyGone());
app.MapPost("/api/save-editor-document", () => LegacyGone());
app.MapPost("/api/document/new", () => LegacyGone());
app.MapPost("/api/export", () => LegacyGone());
app.MapPost(AuthorityRegisterRoute, (HttpContext context, AuthorityRegisterRequest? request) =>
{
    try
    {
        if (request is null) throw new InvalidOperationException("WB-AUTHORITY-400: 注册请求不能为空。");
        MarkAuthorityMutation(context);
        var result = authority.RegisterDocument(request.OperationId ?? string.Empty, request.Path ?? string.Empty);
        return Results.Json(new JsonObject { ["ok"] = true, ["document_revision"] = AuthorityPublicProjection.Document(result, workspace.Root) });
    }
    catch (InvalidOperationException ex) { return AuthorityFailureForContext(ex, context); }
});
app.MapPost(AuthoritySelectionRoute, (HttpContext context, AuthoritySelectionRequest? request) =>
{
    try
    {
        if (request is null) throw new InvalidOperationException("WB-AUTHORITY-400: selection 请求不能为空。");
        MarkAuthorityMutation(context);
        var result = authority.MaterializeSelection(request.OperationId ?? string.Empty, request.DocumentIds ?? []);
        return Results.Json(new JsonObject { ["ok"] = true, ["selection"] = AuthorityPublicProjection.Selection(result) });
    }
    catch (InvalidOperationException ex) { return AuthorityFailureForContext(ex, context); }
});
app.MapPost(AuthorityApprovalRoute, (HttpContext context, string selectionId, AuthorityOperationRequest? request) =>
{
    try
    {
        if (request is null) throw new InvalidOperationException("WB-AUTHORITY-400: approval 请求不能为空。");
        MarkAuthorityMutation(context);
        var result = authority.ApproveSelection(request.OperationId ?? string.Empty, selectionId);
        return Results.Json(new JsonObject { ["ok"] = true, ["approval_proof"] = AuthorityPublicProjection.Approval(result) });
    }
    catch (InvalidOperationException ex) { return AuthorityFailureForContext(ex, context); }
});
app.MapPost(AuthorityCompileProofRoute, (HttpContext context, AuthorityCompileProofRequest? request) =>
{
    try
    {
        if (request is null) throw new InvalidOperationException("WB-AUTHORITY-400: CompileProof 请求不能为空。");
        MarkAuthorityMutation(context);
        var result = authority.IssueCompileProof(request.OperationId ?? string.Empty, request.ApprovalId ?? string.Empty, request.ContentTier ?? "base");
        return Results.Json(new JsonObject { ["ok"] = true, ["compile_proof"] = AuthorityPublicProjection.CompileProof(result) });
    }
    catch (InvalidOperationException ex) { return AuthorityFailureForContext(ex, context); }
});
app.MapGet(AuthorityOperationsRoute, (HttpContext context, string operationId) =>
{
    try { return Results.Ok(new { ok = true, operation = CompileSettlementSupport.PublicOperationProjection(authority.ReadOperation(operationId)) }); }
    catch (InvalidOperationException ex) { return AuthorityFailureForContext(ex, context); }
});
app.MapPost(AuthorityExportRoute, (HttpContext context, AuthorityExportRequest? request) =>
{
    try
    {
        if (request is null) throw new InvalidOperationException("WB-AUTHORITY-400: staging 请求不能为空。");
        MarkAuthorityMutation(context);
        var path = authority.ExportStaging(request.OperationId ?? string.Empty, request.CompileProofId ?? string.Empty, request.ConfirmationToken, request.Output);
        var staging = AuthorityPublicProjection.Artifact("staging", path, workspace.Root)["staging"]?.GetValue<string>();
        return Results.Json(new JsonObject { ["ok"] = true, ["staging"] = staging });
    }
    catch (InvalidOperationException ex) { return AuthorityFailureForContext(ex, context); }
});
app.MapPost(AuthorityPublishRoute, (HttpContext context, AuthorityPublishRequest? request) =>
{
    try
    {
        if (request is null) throw new InvalidOperationException("WB-AUTHORITY-400: publish 请求不能为空。");
        MarkAuthorityMutation(context);
        var path = authority.PublishStaging(request.OperationId ?? string.Empty, request.StagingPath ?? string.Empty, request.ManifestHash ?? string.Empty, request.ExpectedCurrentManifestHash);
        return Results.Json(new JsonObject { ["ok"] = true, ["pointer"] = AuthorityPublicProjection.Artifact("pointer", path, workspace.Root)["pointer"] });
    }
    catch (InvalidOperationException ex) { return AuthorityFailureForContext(ex, context); }
});
app.MapPost(CustomerCompileRoute, (HttpContext context, AuthorityCustomerCompileRequest? request) =>
{
    try
    {
        if (request is null) throw new InvalidOperationException("WB-AUTHORITY-400: compile 请求不能为空。");
        EnsureCustomerRequestFields(request.Extra, "compile");
        if (string.IsNullOrWhiteSpace(request.CompileProofId)) throw new InvalidOperationException("WB-AUTHORITY-400: compile_proof_id 不能为空。");
        MarkAuthorityMutation(context);
        var settlement = authority.CompileApproved(request.CompileProofId, request.ConfirmationToken, request.Output);
        var envelope = settlement.PersistedEnvelope?.DeepClone()?.AsObject() ?? throw new InvalidOperationException("WB-AUTHORITY-OPERATION-409: compile settlement envelope 缺失。");
        return Results.Ok(CompileSettlementSupport.PublicProjection(envelope));
    }
    catch (InvalidOperationException ex) { return AuthorityFailureForContext(ex, context); }
});
app.MapPost(CustomerSaveRoute, (SaveAuthoringRequest? request) =>
{
    try
    {
        if (request is null) throw new AuthoringSaveFailureException("WB-AUTHORING-SAVE-400", 400, "高级保存请求无效。" );
        var saved = service.SaveAdvancedDocumentWithEvidence(request.Path ?? string.Empty, request.Content ?? string.Empty, request.SourceHash ?? string.Empty, request.Revision ?? 0);
        var document = saved.Document;
        var sourceHash = document.Report.InputHash ?? Hashing.Sha256Text(document.Content);
        var revision = WorldbookInputNormalization.ReadRevision(document.Document);
        return Results.Ok(new
        {
            ok = true,
            path = document.Path,
            content = document.Content,
            hash = sourceHash,
            sourceHash,
            revision,
            expectedContentHash = saved.ExpectedContentHash,
            diagnostics = document.Report.Diagnostics,
            document
        });
    }
    catch (AuthoringSaveFailureException ex) { return AuthoringFailure(ex); }
    catch (InvalidOperationException ex) { return AuthoringFailureFromOperation(ex); }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or TimeoutException) { return AuthoringFailure(UnknownAuthoringFailure()); }
});
app.MapPost(CustomerSaveCheckRoute, (SaveAuthoringRequest? request) =>
{
    try
    {
        if (request is null) throw new AuthoringSaveFailureException("WB-AUTHORING-SAVE-400", 400, "高级保存检查请求无效。" );
        var result = service.CheckAdvancedSave(request.Path ?? string.Empty, request.Content ?? string.Empty, request.SourceHash ?? string.Empty, request.Revision ?? 0);
        var document = result.Document;
        var sourceHash = document?.Report.InputHash;
        var revision = document is null ? (int?)null : WorldbookInputNormalization.ReadRevision(document.Document);
        return Results.Ok(new
        {
            ok = true,
            status = result.Status,
            editorDocument = result.EditorDocument,
            document,
            path = document?.Path,
            content = document?.Content,
            hash = sourceHash,
            sourceHash,
            revision,
            expectedContentHash = result.ExpectedContentHash,
            diagnostics = document?.Report.Diagnostics
        });
    }
    catch (AuthoringSaveFailureException ex) { return AuthoringFailure(ex); }
    catch (InvalidOperationException ex) { return AuthoringFailureFromOperation(ex); }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or TimeoutException) { return AuthoringFailure(UnknownAuthoringFailure()); }
});
app.MapPost(CustomerEditorSaveRoute, (SaveEditorDocumentRequest request) =>
{
    if (request is null || string.IsNullOrWhiteSpace(request.Path) || request.Model is null || string.IsNullOrWhiteSpace(request.SourceHash) || request.Registry is null)
        return Results.BadRequest(new { ok = false, error = "WB-EDITOR-SAVE-400", message = "作者模式保存请求不完整。" });
    try
    {
        var snapshot = service.SaveEditorDocument(request.Path, request.Model, request.SourceHash, request.Revision, request.Registry, request.TaxonomyVersion, request.TaxonomyHash);
        return Results.Ok(new { ok = true, editorDocument = snapshot });
    }
    catch (InvalidOperationException ex) { return EditorFailure(ex); }
});
app.MapPost(CustomerCreateRoute, (NewDocumentRequest request) =>
{
    if (request is null) return Results.BadRequest(new { ok = false, error = "WB-DOC-400: 请求不能为空。" });
    if (string.IsNullOrWhiteSpace(request.Subdomain)) return Results.Json(new { ok = false, error = "WB-TAXONOMY-422", message = "新档案必须选择二级主题。" }, statusCode: StatusCodes.Status422UnprocessableEntity);
    try
    {
        var document = string.IsNullOrWhiteSpace(request.Path) && string.IsNullOrWhiteSpace(request.DocumentId)
            ? service.CreateGeneratedDocument(request.Title ?? string.Empty, request.Domain ?? string.Empty, request.ContentTier ?? "base", request.AuthorId ?? "author.developer", request.Subdomain, request.RelatedDomains, request.Era)
            : !string.IsNullOrWhiteSpace(request.Path) && !string.IsNullOrWhiteSpace(request.DocumentId)
                ? service.CreateDocument(request.Path, request.DocumentId, request.Title ?? string.Empty, request.Domain ?? string.Empty, request.ContentTier ?? "base", request.AuthorId ?? "author.developer", request.Subdomain, request.RelatedDomains, request.Era)
                : throw new InvalidOperationException("WB-DOC-400: 新建档案请求不完整。");
        return Results.Ok(new { ok = true, document });
    }
    catch (InvalidOperationException ex) { return AiFailure(ex); }
});
app.MapPost(CustomerExportRoute, (HttpContext context, AuthorityCustomerExportRequest? request) =>
{
    try
    {
        if (request is null) throw new InvalidOperationException("WB-AUTHORITY-400: export 请求不能为空。");
        EnsureCustomerRequestFields(request.Extra, "export-staging");
        if (string.IsNullOrWhiteSpace(request.CompileProofId)) throw new InvalidOperationException("WB-AUTHORITY-400: compile_proof_id 不能为空。");
        MarkAuthorityMutation(context);
        var path = authority.ExportStagingApproved(request.CompileProofId, request.ConfirmationToken, request.Output);
        return Results.Ok(new { ok = true, compileProofId = request.CompileProofId, staging = path });
    }
    catch (InvalidOperationException ex) { return AuthorityFailureForContext(ex, context); }
});
app.MapGet("/api/confirmation-token", (string? contentTier) => Results.Ok(new { contentTier = contentTier ?? "adult_optional", confirmationToken = service.GenerateConfirmationToken(contentTier ?? "adult_optional") }));
app.MapGet("/api/preview", (string? profile, string? fixture, string? path, string? sourceHash, int? revision) =>
{
    try
    {
        RequireDocumentVersion(service, path, sourceHash, revision);
        return Results.Ok(service.Preview(profile ?? "profile.commoner", fixture ?? "preview.default").Envelope);
    }
    catch (InvalidOperationException ex) { return AiFailure(ex); }
});

app.MapPost("/api/ai/session/bootstrap", (HttpContext context) =>
{
    try
    {
        var session = sessions.Bootstrap(context);
        return Results.Ok(new { ok = true, csrfToken = session.CsrfToken, expiresAt = session.ExpiresAt, origin = sessions.Origin });
    }
    catch (InvalidOperationException ex) { return AiFailure(ex); }
});
app.MapGet("/api/ai/providers", (HttpContext context) =>
{
    try
    {
        sessions.RequireReadSession(context);
        var settings = ProviderConfiguration.FromEnvironment();
        return Results.Ok(new
        {
            providers = new[] { settings.GetStatus("local"), settings.GetStatus("cloud") }
        });
    }
    catch (InvalidOperationException ex) { return AiFailure(ex); }
});
app.MapGet("/api/ai/provider-settings", (HttpContext context) =>
{
    try
    {
        sessions.RequireReadSession(context);
        return Results.Ok(ProviderSettingsView());
    }
    catch (InvalidOperationException ex) { return AiFailure(ex); }
});
app.MapPut("/api/ai/provider-settings", (HttpContext context, ProviderSettingsRequest request) =>
{
    try
    {
        sessions.RequireSession(context);
        if (request is null) throw new InvalidOperationException("WB-AI-SETTINGS-422: 云端 Provider 设置不能为空。");
        if (request.ClearSavedConfiguration)
        {
            ProviderSettingsStore.Delete();
        }
        else if (request.ClearSavedLocalWorkerConfiguration)
        {
            ProviderSettingsStore.ClearLocalWorker();
        }
        else
        {
            var apiKey = request.ApiKey?.Trim();
            if (string.IsNullOrWhiteSpace(apiKey) && ProviderSettingsStore.TryLoad(out var existing)) apiKey = existing.ApiKey;
            ProviderSettingsStore.Save(request.BaseUrl ?? string.Empty, request.Model ?? string.Empty, apiKey ?? string.Empty);
            var localWorkerUrl = request.LocalWorkerUrl?.Trim();
            var localWorkerSecret = request.LocalWorkerSecret;
            if (!string.IsNullOrWhiteSpace(localWorkerUrl) || !string.IsNullOrWhiteSpace(localWorkerSecret))
            {
                if (string.IsNullOrWhiteSpace(localWorkerUrl))
                    throw new InvalidOperationException("WB-AI-SETTINGS-422: 填写本机 Worker 凭据时必须同时填写地址。");
                if (string.IsNullOrWhiteSpace(localWorkerSecret))
                {
                    if (!ProviderSettingsStore.TryLoadLocalWorker(out _, out var savedSecret)) throw new InvalidOperationException("WB-AI-SETTINGS-422: 本机 Worker 凭据不能为空。");
                    localWorkerSecret = savedSecret;
                }
                ProviderSettingsStore.SaveLocalWorker(localWorkerUrl, localWorkerSecret);
            }
        }
        return Results.Ok(ProviderSettingsView());
    }
    catch (InvalidOperationException ex) { return AiFailure(ex); }
});
app.MapPost("/api/ai/consent-preview", (HttpContext context, AiConsentPreviewRequest request) =>
{
    try
    {
        if (request is null) throw new InvalidOperationException("WB-AI-REQUEST-400: AI 同意预览请求不能为空。");
        var sessionId = sessions.RequireSession(context);
        var focus = ParseFocus(request.Focus, request.Analysis);
        var analysis = AssistanceFocusMapping.ToAnalysis(focus);
        var providerId = WorldbookInputNormalization.NormalizeProvider(request.ProviderId);
        var settings = ProviderConfiguration.FromEnvironment();
        var providerStatus = settings.GetStatus(providerId);
        if (!string.Equals(providerStatus.State, "configured", StringComparison.Ordinal))
            throw new InvalidOperationException($"WB-AI-PROVIDER-409: {providerStatus.Label}。");
        var current = assistance.CreateRequest(request.Path, providerId, analysis, focus);
        var bufferId = "buffer-" + Guid.NewGuid().ToString("N");
        var consent = sessions.IssueConsent(sessionId, current, bufferId);
        var fields = current.DocumentProjection.Select(item => item.Key).OrderBy(item => item, StringComparer.Ordinal).ToArray();
        return Results.Ok(new
        {
            ok = true,
            consentToken = consent.Token,
            provider = providerStatus,
            analysis = AssistanceAnalysisNames.ToWire(analysis),
            focus = AssistanceAnalysisNames.ToFocusWire(focus),
            scope = new
            {
                documentId = current.DocumentId,
                sourceDocumentHash = current.SourceDocumentHash,
                savedRevision = current.SavedRevision,
                fields,
                note = "只发送已保存当前档案的结构化投影与有限注册表摘要；不发送来源正文、其他档案或未保存编辑。"
            },
            bufferId,
            expiresAt = consent.ExpiresAt
        });
    }
    catch (InvalidOperationException ex) { return AiFailure(ex); }
});
app.MapPost("/api/ai/analyze", async (HttpContext context, AiAnalyzeRequest request, CancellationToken cancellationToken) =>
{
    try
    {
        var sessionId = sessions.RequireSession(context);
        var consent = sessions.ConsumeConsent(request?.ConsentToken ?? string.Empty, sessionId);
        var current = assistance.CreateRequest(consent.Request.DocumentPath, consent.Request.ProviderId, consent.Request.Analysis, consent.Request.Focus);
        if (!string.Equals(current.RequestHash, consent.Request.RequestHash, StringComparison.Ordinal)
            || !string.Equals(current.SourceDocumentHash, consent.Request.SourceDocumentHash, StringComparison.Ordinal)
            || current.SavedRevision != consent.Request.SavedRevision)
            throw new InvalidOperationException("WB-AI-CAS-409: 同意预览后档案已发生变化，请重新预览范围。");
        var provider = AssistanceProviderFactory.Create(current.ProviderId);
        var result = await provider.AnalyzeAsync(current, cancellationToken).ConfigureAwait(false);
        var saved = suggestions.Save(current, result, sessionId, consent.BufferId);
        return Results.Ok(new
        {
            ok = true,
            requestHash = current.RequestHash,
            sourceDocumentHash = current.SourceDocumentHash,
            savedRevision = current.SavedRevision,
            bufferId = consent.BufferId,
            suggestions = saved.Select(ToSuggestionDto).ToArray()
        });
    }
    catch (InvalidOperationException ex) { return AiFailure(ex); }
});
app.MapPost("/api/ai/apply", () => LegacyGone());
app.MapPost("/api/ai/reject", () => LegacyGone());
app.MapAuthoringDraftEndpoints(sessions, drafts, assistance, service, AiFailure);
app.MapAuthoringDraftEndpoints(sessions, drafts, assistance, service, AiFailure, "/api/ai/authoring/draft");
WorkstationHandoffEndpoints.Map(app, sessions, handoffs, handoffFaultInjection);
BatchEndpoints.Map(app, runtime, workspace, service, sessions);
BatchEndpoints.Map(app, runtime, workspace, service, sessions, BatchRoutePrefix);
app.MapPost(CustomerAiApplyRoute, (HttpContext context, AiApplyRequest request) =>
{
    try
    {
        var sessionId = sessions.RequireSession(context);
        if (request is null || string.IsNullOrWhiteSpace(request.Path)) throw new InvalidOperationException("WB-AI-CAS-400: 应用请求不完整。");
        var document = service.ReadDocument(request.Path);
        AiCandidateGuards.EnsureApplyAllowed(document.Document);
        var documentId = document.Document["id"]?.GetValue<string>() ?? throw new InvalidOperationException("WB-AI-CAS-409: 当前档案缺少稳定 ID。");
        var sourceHash = document.Report.InputHash ?? Hashing.FileSha256(document.AbsolutePath);
        var revision = WorldbookInputNormalization.ReadRevision(document.Document);
        var envelope = suggestions.Read(request.SuggestionId, documentId, sourceHash, revision, sessionId, request.BufferId);
        var buffer = DocumentCas.Open(document.Path, documentId, sourceHash, revision, request.BufferId, document.Document);
        var updated = DocumentCas.Apply(buffer, envelope, request.ApplyNonce);
        suggestions.ConsumeApplyNonce(request.SuggestionId, documentId, sourceHash, revision, sessionId, request.BufferId, request.ApplyNonce);
        suggestions.InvalidatePendingForBuffer(documentId, sourceHash, revision, sessionId, request.BufferId);
        var editorDocument = service.ProjectEditorBuffer(document, updated.Document.AsObject());
        var authorTarget = AuthoringCandidateProjection.Project(envelope.Suggestion.Patch);
        return Results.Ok(new
        {
            ok = true,
            saved = false,
            bufferId = updated.BufferId,
            revision = updated.Revision,
            content = service.FormatAuthoringDocument(updated.Document.AsObject(), document.Path),
            editorDocument,
            canApplyToAuthorMode = authorTarget is not null,
            authorTarget = authorTarget is null ? null : new
            {
                step = authorTarget.Step,
                stepTitle = authorTarget.StepTitle,
                label = authorTarget.Label,
                focusId = authorTarget.FocusId,
                operationCount = authorTarget.OperationCount
            },
            note = "建议只应用到编辑器缓冲区；尚未写入正典文件。"
        });
    }
    catch (InvalidOperationException ex) { return AiFailure(ex); }
});
app.MapPost(CustomerAiRejectRoute, (HttpContext context, AiRejectRequest request) =>
{
    try
    {
        var sessionId = sessions.RequireSession(context);
        if (request is null || string.IsNullOrWhiteSpace(request.Path)) throw new InvalidOperationException("WB-AI-CAS-400: 撤销请求不完整。");
        var document = service.ReadDocument(request.Path);
        var documentId = document.Document["id"]?.GetValue<string>() ?? throw new InvalidOperationException("WB-AI-CAS-409: 当前档案缺少稳定 ID。");
        var sourceHash = document.Report.InputHash ?? Hashing.FileSha256(document.AbsolutePath);
        var revision = WorldbookInputNormalization.ReadRevision(document.Document);
        suggestions.Reject(request.SuggestionId, documentId, sourceHash, revision, sessionId, request.BufferId, request.ApplyNonce);
        return Results.Ok(new { ok = true, rejected = true });
    }
    catch (InvalidOperationException ex) { return AiFailure(ex); }
});

app.Run();

static IResult LegacyGone()
    => Results.Json(new { ok = false, error = "WB-AUTHORITY-LEGACY-410", status = 410, side_effect = "none" }, statusCode: StatusCodes.Status410Gone);

static void EnsureCustomerRequestFields(IReadOnlyDictionary<string, JsonElement>? extra, string operation)
{
    if (extra is { Count: > 0 })
        throw new InvalidOperationException($"WB-AUTHORITY-422: {operation} 请求包含未允许字段。");
}

static void RequireDocumentVersion(WorldbookApplicationService service, string? path, string? sourceHash, int? revision)
    => service.EnsureDocumentVersion(path ?? string.Empty, sourceHash ?? string.Empty, revision ?? 0);

static AssistanceFocusKind ParseFocus(string? focus, string? legacyAnalysis)
    => string.IsNullOrWhiteSpace(focus)
        ? legacyAnalysis?.Trim().ToLowerInvariant() switch
        {
            "permissions" => AssistanceFocusKind.KnowledgePermissions,
            "metadata" => AssistanceFocusKind.Metadata,
            "prose" => AssistanceFocusKind.WorldStyle,
            null or "" or "consistency" => AssistanceFocusKind.General,
            _ => throw new InvalidOperationException("WB-AI-REQUEST-400: AI 分析类型无效。")
        }
        : focus.Trim().ToLowerInvariant() switch
    {
        "general" => AssistanceFocusKind.General,
        "completion" => AssistanceFocusKind.Completion,
        "fact_correction" => AssistanceFocusKind.FactCorrection,
        "knowledge_permissions" => AssistanceFocusKind.KnowledgePermissions,
        "hierarchy" => AssistanceFocusKind.Hierarchy,
        "expression_level" => AssistanceFocusKind.ExpressionLevel,
        "metadata" => AssistanceFocusKind.Metadata,
        "world_style" => AssistanceFocusKind.WorldStyle,
        "npc_voice" => AssistanceFocusKind.NpcVoice,
        "domain_style" => AssistanceFocusKind.DomainStyle,
        _ => throw new InvalidOperationException("WB-AI-REQUEST-400: AI 检查重点无效。")
    };

static object ToSuggestionDto(SuggestionEnvelope envelope)
{
    var semantic = SuggestionSemanticProjection.Project(envelope);
    var authorTarget = AuthoringCandidateProjection.Project(semantic.Patch);
    return new
    {
        id = semantic.Id,
        kind = semantic.Kind,
        severity = semantic.Severity,
        confidence = semantic.Confidence,
        title = semantic.Title,
        reason = semantic.Reason,
        candidateText = semantic.CandidateText,
        patch = semantic.Patch,
        reviewOnly = semantic.ReviewOnly,
        applyNonce = semantic.ApplyNonce,
        suggestionHash = semantic.SuggestionHash,
        canApplyToAuthorMode = authorTarget is not null,
        authorTarget = authorTarget is null ? null : new
        {
            step = authorTarget.Step,
            stepTitle = authorTarget.StepTitle,
            label = authorTarget.Label,
            focusId = authorTarget.FocusId,
            operationCount = authorTarget.OperationCount
        }
    };
}

static IResult AiFailure(InvalidOperationException ex)
{
    var code = ex.Message.Split(':', 2)[0];
    var detail = AiFailureProjection.Detail(ex.Message);
    return Results.Json(
        new { ok = false, error = code, message = SafeMessage(code), detail = detail.Length == 0 ? null : detail },
        statusCode: AiFailureProjection.Status(code));
}

static IResult AuthoringFailure(AuthoringSaveFailureException ex)
{
    var diagnostics = ex.Diagnostics.Count == 0 ? null : ex.Diagnostics.Take(32).ToArray();
    return Results.Json(new
    {
        ok = false,
        error = ex.Code,
        message = SafeMessage(ex.Code),
        diagnostics,
        resultUnknown = ex.ResultUnknown
    }, statusCode: ex.HttpStatus);
}

static IResult AuthoringFailureFromOperation(InvalidOperationException ex)
{
    var code = ex.Message.Split(':', 2)[0];
    var diagnostics = new[] { new Diagnostic(code, "error", ex.Message) };
    var failure = code switch
    {
        "WB-DOC-404" or "WB-AUTHORING-CAS-409" => new AuthoringSaveFailureException("WB-AUTHORING-CAS-409", 409, SafeMessage("WB-AUTHORING-CAS-409"), diagnostics),
        "WB-AUTHORING-PARSE-422" => new AuthoringSaveFailureException("WB-AUTHORING-PARSE-422", 422, SafeMessage("WB-AUTHORING-PARSE-422"), diagnostics),
        "WB-AUTHORING-SCHEMA-422" => new AuthoringSaveFailureException("WB-AUTHORING-SCHEMA-422", 422, SafeMessage("WB-AUTHORING-SCHEMA-422"), diagnostics),
        "WB-AUTHORING-UNKNOWN-503" => UnknownAuthoringFailure(),
        _ when code.StartsWith("WB-PATH-", StringComparison.Ordinal) || code == "WB-SAVE-001" => new AuthoringSaveFailureException("WB-AUTHORING-SAVE-400", 400, SafeMessage("WB-AUTHORING-SAVE-400"), diagnostics),
        _ => new AuthoringSaveFailureException("WB-AUTHORING-SAVE-400", 400, SafeMessage("WB-AUTHORING-SAVE-400"), diagnostics)
    };
    return AuthoringFailure(failure);
}

static AuthoringSaveFailureException UnknownAuthoringFailure()
    => new("WB-AUTHORING-UNKNOWN-503", 503, SafeMessage("WB-AUTHORING-UNKNOWN-503"), [new Diagnostic("WB-AUTHORING-UNKNOWN-503", "warning", SafeMessage("WB-AUTHORING-UNKNOWN-503"))], resultUnknown: true);

static IResult SafeFailure(InvalidOperationException ex)
{
    var code = ex.Message.Split(':', 2)[0];
    var status = code == "WB-TAXONOMY-500" ? StatusCodes.Status500InternalServerError : StatusCodes.Status400BadRequest;
    return Results.Json(new { ok = false, error = code, message = SafeMessage(code) }, statusCode: status);
}

static IResult AuthorityFailureForContext(InvalidOperationException ex, HttpContext context)
{
    var failureContext = context.Items.TryGetValue(AuthorityFailureContextItem, out var value) && value is AuthorityFailureContext selected
        ? selected
        : AuthorityFailureContext.Preflight;
    var projection = AuthorityHttpErrorProjection.Project(ex, failureContext);
    AuthorityHttpErrorProjection.Log(context, ex, failureContext, projection);
    context.Response.Headers["X-AWAKE-Correlation-Id"] = projection.CorrelationId;
    return Results.Json(new
    {
        ok = false,
        error = projection.Error,
        message = projection.Message,
        side_effect = projection.SideEffect,
        correlation_id = projection.CorrelationId
    }, statusCode: projection.StatusCode);
}

static void MarkAuthorityMutation(HttpContext context)
    => context.Items[AuthorityFailureContextItem] = AuthorityFailureContext.MutationUnknown;

static IResult EditorFailure(InvalidOperationException ex)
{
    var code = ex.Message.Split(':', 2)[0];
    var status = code switch
    {
        "WB-CAS-409" or "WB-REGISTRY-CAS-409" or "WB-TAXONOMY-CAS-409" => StatusCodes.Status409Conflict,
        "WB-TAXONOMY-500" => StatusCodes.Status500InternalServerError,
        "WB-EDITOR-SOURCE-403" => StatusCodes.Status403Forbidden,
        "WB-EDITOR-SCHEMA-422" or "WB-REGISTRY-422" or "WB-TAXONOMY-422" or "WB-EDITOR-TAXONOMY-422" => StatusCodes.Status422UnprocessableEntity,
        _ => StatusCodes.Status400BadRequest
    };
    return Results.Json(new { ok = false, error = code, message = SafeMessage(code) }, statusCode: status);
}

static string SafeMessage(string code)
    => code switch
    {
        "WB-DOC-404" => "找不到指定档案。",
        "WB-DOC-400" => "档案请求无效。",
        "WB-DOC-001" => "档案内部编号无效，请重新创建档案。",
        "WB-DOC-002" => "请选择一个有效的知识分类。",
        "WB-DOC-003" => "内容层选择无效，请重新选择。",
        "WB-DOC-004" => "作者设置无效，请重新打开工作室。",
        "WB-DOC-005" => "身份目录当前不可用，请先刷新工作室。",
        "WB-DOC-006" => "暂时无法生成新的档案，请稍后重试。",
        "WB-TAXONOMY-500" => "分类目录不可用，工作室已停止本次操作。",
        "WB-TAXONOMY-CAS-400" => "分类目录快照无效，请重新读取档案。",
        "WB-TAXONOMY-CAS-409" => "分类目录已更新，请刷新后再保存。",
        "WB-TAXONOMY-422" or "WB-EDITOR-TAXONOMY-422" => "二级分类或相关分类选择无效，请按界面提示重新选择。",
        "WB-SAVE-001" => "保存请求无效。",
        "WB-CAS-409" => "档案已被其他操作更新，请重新读取后再保存。",
        "WB-REGISTRY-CAS-409" => "身份或推荐对象目录已更新，请刷新后再保存。",
        "WB-EDITOR-SOURCE-403" => "来源型内容不能应用 AI 候选；需要改动请进入高级模式手工维护。",
        "WB-EDITOR-SCHEMA-422" => "当前内容未通过结构检查，请按提示补全必填项。",
        "WB-AUTHORING-SAVE-400" => "高级保存请求无效，请检查档案路径、内容和版本信息。",
        "WB-AUTHORING-PARSE-422" => "高级文件无法解析，请检查 YAML 或 JSON 格式。",
        "WB-AUTHORING-SCHEMA-422" => "高级文件未通过世界书结构检查，请按诊断提示修正。",
        "WB-AUTHORING-CAS-409" => "档案已经被其他操作更新，请重新读取后再保存。",
        "WB-AUTHORING-UNKNOWN-503" => "文件写入结果暂时无法确认，请检查保存结果。",
        "WB-OP-VERSION-400" => "操作缺少当前档案版本信息，请重新读取档案。",
        "WB-OP-VERSION-409" => "当前档案已经变化，请重新读取后再执行操作。",
        "WB-PATH-002" or "WB-PATH-003" or "WB-PATH-004" or "WB-PATH-005" => "工作区写入路径不允许。",
        "WB-AI-ENDPOINT-403" => "云端 Provider 地址不符合安全策略。",
        "WB-AI-SETTINGS-422" => "云端 Provider 设置不完整或格式不正确。",
        "WB-AI-SETTINGS-500" => "云端 Provider 设置无法安全保存，请检查本机权限。",
        "WB-AI-AUTH-401" or "WB-AI-WORKER-SECRET-401" => "AI Provider 认证失败。",
        "WB-AI-RATE-429" => "AI Provider 暂时达到请求限制。",
        "WB-AI-WORKER-TIMEOUT-408" or "WB-AI-CLOUD-TIMEOUT-408" => "AI 请求等待时间过长，请稍后重试。",
        "WB-AI-WORKER-TRANSPORT-502" or "WB-AI-CLOUD-TRANSPORT-502" => "暂时无法连接 AI Provider，请检查本机 Worker 或云端设置。",
        "WB-AI-DRAFT-400" => "参考资料草稿请求不完整。",
        "WB-AI-DRAFT-404" => "参考资料草稿不存在或已过期，请重新开始。",
        "WB-AI-DRAFT-413" => "参考资料太长，请先分成几份资料。",
        "WB-AI-DRAFT-422" => "草稿内容与当前审核结果不一致，请重新生成或重新采纳相关条目。",
        "WB-AI-REVIEW-CANDIDATE-409" or "WB-AI-REVIEW-CAS-409" or "WB-AI-DRAFT-CANDIDATE-409" => "当前候选已经变化或失效，请刷新后重新选择。",
        "WB-AI-REVIEW-422" => "审查操作与当前候选集不一致，请刷新候选后重试。",
        "WB-AI-DRAFT-CAS-409" => "参考资料在本次创作中发生变化，请重新开始。",
        "WB-AI-DRAFT-BIND-409" => "AI 返回结果与当前草稿不匹配，请重新生成。",
        "WB-AI-DRAFT-FORMAT-JSON" => "AI 返回的草稿格式无法识别，请重试或更换 Provider。",
        "WB-AI-DRAFT-MODE-501" => "Semantic Migration 使用独立迁移工作流，当前 Quick Authoring 不处理该模式。",
        "WB-AI-DRAFT-TIER-422" => "请先明确选择内容层级，再创建作者草稿。",
        "WB-AI-DRAFT-GOAL-422" => "请先说明希望整理什么内容。",
        "WB-AI-DRAFT-EXPRESSIONS-422" => "本次未请求身份表达，AI 返回了越界内容；请重新生成。",
        "WB-AI-DRAFT-PERSPECTIVES-413" => "身份视角数量超出上限，请减少视角后重新生成。",
        "WB-AI-DRAFT-409" => "本次生成已有进行中的尝试或缺少尝试编号，请先查看当前状态。",
        "WB-AI-DRAFT-EDIT-409" => "找不到本次修改对应的原始事实或身份表达，请重新生成草稿。",
        "WB-AI-DRAFT-EDIT-422" => "修改内容无法在原始证据中定位，或触发了禁止新增约束，已阻断建档。",
        "WB-AI-DRAFT-RETRY-409" => "只能重试当前资料对应的失败或未结算尝试。",
        "WB-AI-DRAFT-STATE-409" => "草稿状态文件版本不受支持，请重新开始本次创作。",
        "WB-AI-DRAFT-STATE-422" => "草稿状态文件损坏或包含无效记录，请重新开始本次创作。",
        "WB-AI-DRAFT-UNKNOWN-503" => "草稿生成结果暂时无法确认，请稍后在草稿列表核对后再重试。",
        "WB-AI-DRAFT-PROVIDER-CAS-409" => "生成过程中 Provider 设置发生变化，请重新生成草稿。",
        "WB-AI-WORKER-HANDSHAKE-409" => "本机 Worker 拒绝了本次握手（nonce 已使用），请重新发起请求。",
        _ when code.StartsWith("WB-AI-DRAFT-PASS-", StringComparison.Ordinal) => "AI 整理结果未通过来源绑定检查，未生成可用草稿；具体原因见下方说明。",
        _ => "请求无法完成，请检查档案校验结果。"
    };

public sealed record SaveAuthoringRequest(string? Path, string? Content, string? SourceHash, int? Revision);
public sealed record SaveEditorDocumentRequest(string? Path, JsonObject? Model, string? SourceHash, int Revision, JsonObject? Registry, string? TaxonomyVersion, string? TaxonomyHash);
public sealed record ExportRequest(string? ContentTier, string? ConfirmationToken, string? Output, string? Path, string? SourceHash, int? Revision);
public sealed record AuthorityOperationRequest(string? OperationId);
public sealed record AuthorityRegisterRequest(string? OperationId, string? Path);
public sealed record AuthoritySelectionRequest(string? OperationId, List<string>? DocumentIds);
public sealed record AuthorityCompileProofRequest(string? OperationId, string? ApprovalId, string? ContentTier);
public sealed record AuthorityCompileRequest(string? CompileProofId, string? ConfirmationToken, string? Output);
public sealed record AuthorityExportRequest(string? OperationId, string? CompileProofId, string? ConfirmationToken, string? Output);
public sealed class AuthorityCustomerCompileRequest
{
    [JsonPropertyName("compile_proof_id")] public string? CompileProofId { get; set; }
    [JsonPropertyName("confirmation_token")] public string? ConfirmationToken { get; set; }
    [JsonPropertyName("output")] public string? Output { get; set; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}
public sealed class AuthorityCustomerExportRequest
{
    [JsonPropertyName("compile_proof_id")] public string? CompileProofId { get; set; }
    [JsonPropertyName("confirmation_token")] public string? ConfirmationToken { get; set; }
    [JsonPropertyName("output")] public string? Output { get; set; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}
public sealed record AuthorityPublishRequest(string? OperationId, string? StagingPath, string? ManifestHash, string? ExpectedCurrentManifestHash);
public sealed record NewDocumentRequest(string? Path, string? DocumentId, string? Title, string? Domain, string? Subdomain, List<string>? RelatedDomains, string? ContentTier, string? AuthorId, string? Era = null);
public sealed record AiConsentPreviewRequest(string Path, string ProviderId, string Analysis, string? Focus = null);
public sealed record ProviderSettingsRequest(
    string? BaseUrl,
    string? Model,
    string? ApiKey,
    bool ClearSavedConfiguration,
    string? LocalWorkerUrl = null,
    string? LocalWorkerSecret = null,
    bool ClearSavedLocalWorkerConfiguration = false);
public sealed record AiAnalyzeRequest(string ConsentToken);
public sealed record AiApplyRequest(string Path, string BufferId, string SuggestionId, string ApplyNonce);
public sealed record AiRejectRequest(string Path, string BufferId, string SuggestionId, string ApplyNonce);
internal sealed record DraftPrepareRequest(
    string? DraftId,
    string? ProviderId,
    string? Stage,
    string? SourceName,
    string? SourceNature,
    string? SourceText,
    List<AuthoringDraftFact>? AcceptedFacts,
    AuthoringDraftMetadata? Metadata,
    List<string>? Perspectives,
    string? RetryOfAttemptId,
    string? CandidateId,
    string? Mode = null,
    string? AuthoringGoal = null,
    string? UserInstruction = null,
    string? RequestedDomain = null,
    string? RequestedSubdomain = null,
    List<string>? RequestedAudience = null,
    List<string>? RequestedPerspectives = null,
    List<string>? StyleConstraints = null,
    List<string>? MustPreserve = null,
    List<string>? MustNotInvent = null,
    string? RequestedContentTier = null,
    string? RequestedEntryKind = null,
    bool? AdultConfirmed = null);
internal sealed record DraftGenerateRequest(string? DraftToken, string? AttemptId);
internal sealed record DraftCreateDocumentRequest(
    string? DraftId,
    string? CandidateId,
    string? Title,
    string? Summary,
    string? Domain,
    List<AuthoringDraftFact>? Facts,
    List<AuthoringDraftExpression>? Expressions,
    string? ContentTier = null,
    string? Subdomain = null,
    List<string>? RelatedDomains = null,
    bool? AdultConfirmed = null,
    string? Era = null);
internal sealed record DraftReviewDecisionRequest(
    string? DraftId,
    string? Operation,
    List<string>? CandidateIds,
    List<string>? OrderedCandidateIds,
    List<List<string>>? SplitGroups,
    string? OperationId = null,
    string? ExpectedGenerationId = null,
    string? ExpectedSourceContentHash = null,
    string? ExpectedPacketHash = null);

internal static class AiFailureProjection
{
    private const int MaximumDetailLength = 400;

    public static int Status(string code)
        => code switch
        {
            "WB-AI-CSRF-403" or "WB-AI-ENDPOINT-403" or "WB-AI-PATCH-403" or "WB-AI-WORKER-HANDSHAKE-403" or "WB-EDITOR-SOURCE-403" => 403,
            "WB-AI-AUTH-401" or "WB-AI-WORKER-SECRET-401" => 401,
            "WB-AI-CONSENT-404" or "WB-AI-SUGGESTION-404" or "WB-AI-DRAFT-404" => 404,
            "WB-AI-RATE-429" => 429,
            "WB-AI-WORKER-TIMEOUT-408" or "WB-AI-CLOUD-TIMEOUT-408" => 408,
            "WB-AI-WORKER-TRANSPORT-502" or "WB-AI-CLOUD-TRANSPORT-502" => 502,
            "WB-AI-RESPONSE-413" or "WB-AI-SUGGESTION-413" or "WB-AI-DRAFT-413" => 413,
            "WB-AI-DRAFT-422" or "WB-AI-REVIEW-422" or "WB-AI-DRAFT-GOAL-422" or "WB-AI-DRAFT-TIER-422" or "WB-AI-DRAFT-EXPRESSIONS-422" => 422,
            "WB-AI-REVIEW-CANDIDATE-409" or "WB-AI-REVIEW-CAS-409" or "WB-AI-DRAFT-CANDIDATE-409" => 409,
            "WB-AI-CAS-409" or "WB-AI-BIND-409" or "WB-AI-PROVIDER-409" or "WB-AI-DRAFT-CAS-409" or "WB-AI-DRAFT-BIND-409" or "WB-AI-DRAFT-CANDIDATE-409" => 409,
            "WB-OP-VERSION-409" => 409,
            "WB-AI-DRAFT-MODE-501" => 501,
            "WB-TAXONOMY-500" => 500,
            _ when code.StartsWith("WB-AI-DRAFT-PASS-", StringComparison.Ordinal) => PassStatus(code),
            _ => 400
        };

    private static int PassStatus(string code)
        => code.EndsWith("-409", StringComparison.Ordinal) ? 409
            : code.EndsWith("-400", StringComparison.Ordinal) ? 400
            : 422;

    public static string Detail(string message)
    {
        var separator = message.IndexOf(':');
        if (separator < 0 || separator + 1 >= message.Length) return string.Empty;
        var detail = message[(separator + 1)..].Trim();
        return detail.Length > MaximumDetailLength ? detail[..MaximumDetailLength] : detail;
    }
}
