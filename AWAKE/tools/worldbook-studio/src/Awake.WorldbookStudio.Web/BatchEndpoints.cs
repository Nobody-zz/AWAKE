using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.Features;

using Awake.WorldbookStudio.Core;

namespace Awake.WorldbookStudio.Web;

internal static class BatchEndpoints
{
    private const string OwnerId = "author.developer";
    private const string ContractFileName = "awake.worldbook.batch-authoring-contracts-r14.json";
    private static readonly string[] RouteNames =
    [
        "scan", "create", "get", "consent", "start", "pause", "cancel", "claim",
        "retry-item", "review", "create-documents", "report", "item-detail", "source-unit", "prebatch-source-unit"
    ];

    public static void Map(
        WebApplication app,
        WebRuntimeContext runtime,
        WorkspaceService workspace,
        WorldbookApplicationService documents,
        WebAiSessionStore sessions,
        string routePrefix = "/api/ai/batch")
    {
        var contract = BatchAuthoringContractRegistry.Load(FindContractPath(runtime));
        foreach (var routeName in RouteNames) _ = contract.RequireRoute(routeName);
        var ownerInstanceId = runtime.Bootstrap.InstanceId;
        var workspaceMarkerHash = Hashing.Sha256Text(StudioRuntimeHashing.NormalizePathForIdentity(runtime.WorkspaceRoot));
        var consents = new BatchConsentService(workspace, contract, OwnerId, ownerInstanceId, workspaceMarkerHash);
        var workflow = new BatchWorkflowService(workspace, contract, OwnerId, ownerInstanceId, workspaceMarkerHash, consents);
        var controls = new BatchControlService(workspace, contract, OwnerId, ownerInstanceId, workspaceMarkerHash);
        var batchDocuments = new BatchDocumentService(workspace, documents, OwnerId);

        app.MapPost($"{routePrefix}/scan", async (HttpContext context) =>
        {
            try
            {
                sessions.RequireSession(context);
                var inputs = await ReadUploads(context.Request).ConfigureAwait(false);
                var scan = workflow.Scan(inputs, OwnerId, ownerInstanceId, workspaceMarkerHash);
                return Wrap(contract, "scan", BatchPublicProjection.ProjectScan(scan.Scan));
            }
            catch (InvalidOperationException ex) { return Failure(ex); }
        });

        app.MapPost($"{routePrefix}/create", (HttpContext context, BatchCreateApiRequest? request) =>
        {
            try
            {
                sessions.RequireSession(context);
                if (request is null || request.ModelParameters is null) throw new InvalidOperationException("WB-BATCH-REQUEST-400: 创建请求不完整。");
                var provider = CreateProvider(workspace, contract, ownerInstanceId, workspaceMarkerHash);
                var fingerprint = provider.ResolveFingerprint(request.ProviderId ?? string.Empty, request.ModelParameters);
                var created = workflow.Create(
                    new BatchCreateRequest(
                        request.ScanId ?? string.Empty,
                        request.ExpectedScanHash ?? string.Empty,
                        request.PipelineRevision ?? "batch-authoring.v1",
                        request.ProviderId ?? string.Empty,
                        request.ModelParameters,
                        request.IdempotencyKey ?? string.Empty),
                    fingerprint);
                return Wrap(contract, "create", created.PublicManifest);
            }
            catch (InvalidOperationException ex) { return Failure(ex); }
        });

        app.MapGet($"{routePrefix}/{{batchId}}", (HttpContext context, string batchId) =>
        {
            try
            {
                sessions.RequireReadSession(context);
                return Wrap(contract, "get", workflow.GetPublicManifest(batchId));
            }
            catch (InvalidOperationException ex) { return Failure(ex); }
        });

        app.MapGet($"{routePrefix}/{{batchId}}/items/{{itemId}}", (HttpContext context, string batchId, string itemId) =>
        {
            try
            {
                sessions.RequireReadSession(context);
                return Wrap(contract, "item-detail", workflow.GetPublicReviewProjection(batchId, itemId));
            }
            catch (InvalidOperationException ex) { return Failure(ex); }
        });

        app.MapGet($"{routePrefix}/{{batchId}}/report", (HttpContext context, string batchId) =>
        {
            try
            {
                sessions.RequireReadSession(context);
                return Wrap(contract, "report", workflow.GetPublicReport(batchId));
            }
            catch (InvalidOperationException ex) { return Failure(ex); }
        });

        app.MapGet($"{routePrefix}/{{batchId}}/sources/{{snapshotId}}/units/{{sourceUnitId}}", (HttpContext context, string batchId, string snapshotId, string sourceUnitId) =>
        {
            try
            {
                sessions.RequireReadSession(context);
                return Wrap(contract, "source-unit", workflow.GetPublicSourceUnit(batchId, snapshotId, sourceUnitId));
            }
            catch (InvalidOperationException ex) { return Failure(ex); }
        });

        app.MapGet($"{routePrefix}/prebatches/{{scanId}}/sources/{{snapshotId}}/units/{{sourceUnitId}}", (HttpContext context, string scanId, string snapshotId, string sourceUnitId) =>
        {
            try
            {
                sessions.RequireReadSession(context);
                return Wrap(contract, "prebatch-source-unit", workflow.GetPublicPrebatchSourceUnit(scanId, snapshotId, sourceUnitId));
            }
            catch (InvalidOperationException ex) { return Failure(ex); }
        });

        app.MapPost($"{routePrefix}/{{batchId}}/consent", (HttpContext context, string batchId, BatchConsentApiRequest? request) =>
        {
            try
            {
                sessions.RequireSession(context);
                if (request is null || request.ModelParameters is null) throw new InvalidOperationException("WB-BATCH-CONSENT-422: 授权请求不完整。");
                var issued = consents.Issue(new BatchConsentRequest(
                    batchId,
                    request.ExpectedRevision,
                    request.ProviderId ?? string.Empty,
                    request.Scope ?? string.Empty,
                    request.ModelParameters,
                    request.SendScope ?? string.Empty,
                    request.AuthorizedItemIds ?? [],
                    request.SelectedSnapshotIds));
                return Wrap(contract, "consent", new JsonObject
                {
                    ["consent"] = issued.PublicConsent.DeepClone(),
                    ["consent_token"] = issued.ConsentToken,
                    ["expires_at"] = issued.ExpiresAt.ToUniversalTime().ToString("O"),
                    ["correlation_id"] = issued.CorrelationId
                });
            }
            catch (InvalidOperationException ex) { return Failure(ex); }
        });

        app.MapPost($"{routePrefix}/{{batchId}}/start", async (HttpContext context, string batchId, BatchStartApiRequest? request, CancellationToken cancellationToken) =>
        {
            try
            {
                sessions.RequireSession(context);
                if (request is null) throw new InvalidOperationException("WB-BATCH-REQUEST-400: 启动请求不完整。");
                var execution = new BatchExecutionService(
                    workspace,
                    contract,
                    consents,
                    OwnerId,
                    ownerInstanceId,
                    workspaceMarkerHash,
                    ProviderConfiguration.FromEnvironment());
                var started = await execution.StartAsync(new BatchStartRequest(
                    batchId,
                    request.ExpectedRevision,
                    request.ConsentToken ?? string.Empty,
                    request.ClaimGeneration,
                    request.ConsentRevision,
                    request.Stage ?? string.Empty,
                    request.TargetItemIds ?? []), cancellationToken).ConfigureAwait(false);
                return Wrap(contract, "start", started.PublicManifest);
            }
            catch (InvalidOperationException ex) { return Failure(ex); }
        });

        app.MapPost($"{routePrefix}/{{batchId}}/pause", (HttpContext context, string batchId, BatchRevisionApiRequest? request) =>
        {
            try { sessions.RequireSession(context); if (request is null) throw new InvalidOperationException("WB-BATCH-REQUEST-400: 暂停请求不完整。"); return Wrap(contract, "pause", controls.ChangeState(batchId, request.ExpectedRevision, "paused").Data); }
            catch (InvalidOperationException ex) { return Failure(ex); }
        });

        app.MapPost($"{routePrefix}/{{batchId}}/cancel", (HttpContext context, string batchId, BatchRevisionApiRequest? request) =>
        {
            try { sessions.RequireSession(context); if (request is null) throw new InvalidOperationException("WB-BATCH-REQUEST-400: 取消请求不完整。"); return Wrap(contract, "cancel", controls.ChangeState(batchId, request.ExpectedRevision, "cancelled").Data); }
            catch (InvalidOperationException ex) { return Failure(ex); }
        });

        app.MapPost($"{routePrefix}/{{batchId}}/claim", (HttpContext context, string batchId, BatchClaimApiRequest? request) =>
        {
            try { sessions.RequireSession(context); if (request is null) throw new InvalidOperationException("WB-BATCH-REQUEST-400: 接管请求不完整。"); return Wrap(contract, "claim", controls.Claim(batchId, request.ExpectedRevision, request.ClaimReason ?? "manual_reclaim").Data); }
            catch (InvalidOperationException ex) { return Failure(ex); }
        });

        app.MapPost($"{routePrefix}/{{batchId}}/items/{{itemId}}/retry", (HttpContext context, string batchId, string itemId, BatchRetryApiRequest? request) =>
        {
            try
            {
                sessions.RequireSession(context);
                if (request is null) throw new InvalidOperationException("WB-BATCH-REQUEST-400: 重试请求不完整。");
                return Wrap(contract, "retry-item", controls.RetryItem(batchId, itemId, request.ExpectedItemRevision, request.ManualConfirmation, request.Stage ?? string.Empty).Data);
            }
            catch (InvalidOperationException ex) { return Failure(ex); }
        });

        app.MapPost($"{routePrefix}/{{batchId}}/items/{{itemId}}/review", (HttpContext context, string batchId, string itemId, BatchReviewApiRequest? request) =>
        {
            try
            {
                sessions.RequireSession(context);
                if (request is null) throw new InvalidOperationException("WB-BATCH-REVIEW-422: 审核请求不完整。");
                var decisions = request.FactDecisions?.Select(decision =>
                {
                    var value = new JsonObject
                    {
                        ["fact_id"] = decision.FactId ?? string.Empty,
                        ["decision"] = decision.Decision ?? string.Empty
                    };
                    if (!string.IsNullOrWhiteSpace(decision.Reason)) value["reason"] = decision.Reason;
                    return value;
                }).ToArray();
                var review = workflow.ReviewFacts(new BatchReviewRequest(
                    batchId,
                    itemId,
                    request.ExpectedItemRevision,
                    request.FactIds ?? [],
                    request.RiskLevel ?? string.Empty,
                    request.ReviewStatus ?? string.Empty,
                    request.ReviewerNote,
                    request.RejectionReason,
                    decisions));
                return Wrap(contract, "review", BatchPublicProjection.ProjectReviewDecision(review));
            }
            catch (InvalidOperationException ex) { return Failure(ex); }
        });

        app.MapPost($"{routePrefix}/{{batchId}}/create-documents", (HttpContext context, string batchId, BatchCreateDocumentsApiRequest? request) =>
        {
            try
            {
                sessions.RequireSession(context);
                if (request is null || request.Items is null) throw new InvalidOperationException("WB-BATCH-EVIDENCE-422: 建档请求不完整。");
                var inputs = request.Items.Select(item => new BatchDocumentCreateInput(
                    item.ItemId ?? string.Empty,
                    item.ExpectedItemRevision,
                    item.FactIds ?? [],
                    item.MetadataSelection ?? new JsonObject())).ToArray();
                return Wrap(contract, "create-documents", batchDocuments.Create(batchId, request.ExpectedRevision, request.CreateMode ?? string.Empty, inputs).Data);
            }
            catch (InvalidOperationException ex) { return Failure(ex); }
        });
    }

    private static BatchProviderService CreateProvider(WorkspaceService workspace, BatchAuthoringContractRegistry contract, string ownerInstanceId, string workspaceMarkerHash)
        => new(
            ProviderConfiguration.FromEnvironment(),
            new BatchCacheRepository(workspace, contract),
            new BatchFactReviewRepository(workspace, contract, OwnerId, ownerInstanceId, workspaceMarkerHash));

    private static async Task<IReadOnlyList<BatchSourceInput>> ReadUploads(HttpRequest request)
    {
        var bodyLimit = request.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (bodyLimit is { IsReadOnly: false }) bodyLimit.MaxRequestBodySize = 64L * 1024 * 1024;
        var form = await request.ReadFormAsync().ConfigureAwait(false);
        if (form.Files.Count is < 1 or > 200) throw new InvalidOperationException("WB-BATCH-UPLOAD-400: 请至少选择一个、最多选择 200 个参考文件。");
        var relativePaths = form["relative_path"];
        var displayNames = form["display_name"];
        var totalBytes = 0L;
        var inputs = new List<BatchSourceInput>(form.Files.Count);
        for (var index = 0; index < form.Files.Count; index++)
        {
            var file = form.Files[index];
            if (file.Length > 16L * 1024 * 1024) throw new InvalidOperationException("WB-BATCH-UPLOAD-413: 单个参考文件不能超过 16 MiB。");
            totalBytes += file.Length;
            if (totalBytes > 64L * 1024 * 1024) throw new InvalidOperationException("WB-BATCH-UPLOAD-413: 参考文件总大小不能超过 64 MiB。");
            await using var stream = file.OpenReadStream();
            using var buffer = new MemoryStream(checked((int)file.Length));
            await stream.CopyToAsync(buffer).ConfigureAwait(false);
            var relativePath = index < relativePaths.Count && !string.IsNullOrWhiteSpace(relativePaths[index]) ? relativePaths[index]! : file.FileName;
            var displayName = index < displayNames.Count && !string.IsNullOrWhiteSpace(displayNames[index]) ? displayNames[index]! : Path.GetFileName(relativePath);
            inputs.Add(new BatchSourceInput(relativePath, displayName, buffer.ToArray()));
        }
        return inputs;
    }

    private static string FindContractPath(WebRuntimeContext runtime)
    {
        var candidates = new[]
        {
            Path.Combine(runtime.SchemaRoot, "batch", ContractFileName),
            Path.Combine(runtime.PackageRoot, "schemas", "batch", ContractFileName)
        };
        foreach (var candidate in candidates) if (File.Exists(candidate)) return candidate;
        var current = Path.GetFullPath(runtime.PackageRoot);
        for (var depth = 0; depth < 12 && !string.IsNullOrWhiteSpace(current); depth++)
        {
            var candidate = Path.Combine(current, "docs", "superpowers", "specs", "2026-08-25-worldbook-studio-batch-authoring-contracts-r14.json");
            if (File.Exists(candidate)) return candidate;
            current = Directory.GetParent(current)?.FullName ?? string.Empty;
        }
        throw new InvalidOperationException("WB-BATCH-CONTRACT-404: 批量作者契约未随 Studio 安装包提供。");
    }

    private static IResult Wrap(BatchAuthoringContractRegistry contract, string routeName, JsonObject data)
    {
        var correlationId = NewId("correlation");
        var route = contract.RequireRoute(routeName);
        var responseReference = route["response_ref"]?.GetValue<string>()
            ?? throw new InvalidOperationException($"WB-BATCH-CONTRACT-422: route {routeName} 缺少 response_ref。");
        var response = new JsonObject
        {
            ["data"] = data,
            ["correlation_id"] = correlationId
        };
        contract.ValidatePayload(responseReference, response);
        return Results.Ok(response);
    }

    private static IResult Failure(InvalidOperationException exception)
    {
        var raw = exception.Message.Split(':', 2)[0];
        var code = raw switch
        {
            "WB-AI-CSRF-403" => "WB-BATCH-CONSENT-403",
            "WB-AI-CONSENT-404" => "WB-BATCH-CONSENT-404",
            _ when raw.StartsWith("WB-BATCH-", StringComparison.Ordinal) => raw,
            _ => "WB-BATCH-INTERNAL-500"
        };
        var status = code switch
        {
            _ when code.EndsWith("-404", StringComparison.Ordinal) => StatusCodes.Status404NotFound,
            _ when code.EndsWith("-403", StringComparison.Ordinal) => StatusCodes.Status403Forbidden,
            _ when code.EndsWith("-401", StringComparison.Ordinal) => StatusCodes.Status401Unauthorized,
            _ when code.EndsWith("-409", StringComparison.Ordinal) || code.Contains("INFLIGHT", StringComparison.Ordinal) => StatusCodes.Status409Conflict,
            _ when code.EndsWith("-413", StringComparison.Ordinal) => StatusCodes.Status413PayloadTooLarge,
            _ when code.EndsWith("-500", StringComparison.Ordinal) => StatusCodes.Status500InternalServerError,
            _ => StatusCodes.Status422UnprocessableEntity
        };
        var retryable = status is StatusCodes.Status408RequestTimeout or StatusCodes.Status429TooManyRequests or StatusCodes.Status500InternalServerError or StatusCodes.Status502BadGateway
            || code.Contains("INFLIGHT", StringComparison.Ordinal)
            || code.Contains("RECONCILE", StringComparison.Ordinal);
        return Results.Json(new
        {
            code,
            message = SafeMessage(code),
            correlation_id = NewId("correlation"),
            retryable,
            http_status = status,
            error_ref = "awake.worldbook.batch-error.v2"
        }, statusCode: status);
    }

    private static string SafeMessage(string code) => code switch
    {
        "WB-BATCH-CONTRACT-404" => "批量作者契约未安装，请重新安装 Studio。",
        "WB-BATCH-UPLOAD-400" or "WB-BATCH-UPLOAD-422" => "参考资料上传格式不正确。",
        "WB-BATCH-UPLOAD-413" => "参考资料太大，请拆分后重试。",
        "WB-BATCH-PROVIDER-422" => "当前 AI Provider 未配置或选择无效。",
        "WB-BATCH-PROVIDER-500" or "WB-BATCH-METADATA-500" => "AI 处理未完成，请检查 Provider 状态后重试。",
        "WB-BATCH-CONSENT-403" or "WB-BATCH-OWNER-403" => "当前操作没有有效授权，请重新打开工作室。",
        "WB-BATCH-CONSENT-404" => "授权已失效，请重新确认 AI 读取范围。",
        "WB-BATCH-REVISION-409" => "批次内容已更新，请刷新后再操作。",
        "WB-BATCH-IDEMPOTENCY-409" => "相同操作编号对应了不同内容，请重新开始。",
        "WB-BATCH-IDEMPOTENCY-INFLIGHT-409" => "相同操作正在处理中，请稍后查看批次状态。",
        "WB-BATCH-EVIDENCE-422" => "事实证据与原文不一致，请检查引用范围。",
        "WB-BATCH-STAGE-422" or "WB-BATCH-STAGE-409" => "当前项目还没有到达这一步。",
        _ => "批量作者任务未完成，请查看项目状态后重试。"
    };

    private static string NewId(string prefix) => $"{prefix}.{Guid.NewGuid():N}";
}

internal sealed record BatchCreateApiRequest(
    [property: JsonPropertyName("scan_id")] string? ScanId,
    [property: JsonPropertyName("expected_scan_hash")] string? ExpectedScanHash,
    [property: JsonPropertyName("pipeline_revision")] string? PipelineRevision,
    [property: JsonPropertyName("provider_id")] string? ProviderId,
    [property: JsonPropertyName("model_parameters")] JsonObject? ModelParameters,
    [property: JsonPropertyName("idempotency_key")] string? IdempotencyKey);
internal sealed record BatchConsentApiRequest(
    [property: JsonPropertyName("expected_revision")] int ExpectedRevision,
    [property: JsonPropertyName("provider_id")] string? ProviderId,
    [property: JsonPropertyName("scope")] string? Scope,
    [property: JsonPropertyName("model_parameters")] JsonObject? ModelParameters,
    [property: JsonPropertyName("send_scope")] string? SendScope,
    [property: JsonPropertyName("authorized_item_ids")] List<string>? AuthorizedItemIds,
    [property: JsonPropertyName("selected_snapshot_ids")] List<string>? SelectedSnapshotIds);
internal sealed record BatchStartApiRequest(
    [property: JsonPropertyName("expected_revision")] int ExpectedRevision,
    [property: JsonPropertyName("consent_token")] string? ConsentToken,
    [property: JsonPropertyName("claim_generation")] int ClaimGeneration,
    [property: JsonPropertyName("consent_revision")] int ConsentRevision,
    [property: JsonPropertyName("stage")] string? Stage,
    [property: JsonPropertyName("target_item_ids")] List<string>? TargetItemIds);
internal sealed record BatchRevisionApiRequest([property: JsonPropertyName("expected_revision")] int ExpectedRevision);
internal sealed record BatchClaimApiRequest(
    [property: JsonPropertyName("expected_revision")] int ExpectedRevision,
    [property: JsonPropertyName("claim_reason")] string? ClaimReason);
internal sealed record BatchRetryApiRequest(
    [property: JsonPropertyName("expected_item_revision")] int ExpectedItemRevision,
    [property: JsonPropertyName("manual_confirmation")] bool ManualConfirmation,
    [property: JsonPropertyName("stage")] string? Stage);
internal sealed record BatchReviewFactDecisionApiRequest(
    [property: JsonPropertyName("fact_id")] string? FactId,
    [property: JsonPropertyName("decision")] string? Decision,
    [property: JsonPropertyName("reason")] string? Reason);
internal sealed record BatchReviewApiRequest(
    [property: JsonPropertyName("expected_item_revision")] int ExpectedItemRevision,
    [property: JsonPropertyName("fact_ids")] List<string>? FactIds,
    [property: JsonPropertyName("risk_level")] string? RiskLevel,
    [property: JsonPropertyName("review_status")] string? ReviewStatus,
    [property: JsonPropertyName("reviewer_note")] string? ReviewerNote,
    [property: JsonPropertyName("rejection_reason")] string? RejectionReason,
    [property: JsonPropertyName("fact_decisions")] List<BatchReviewFactDecisionApiRequest>? FactDecisions);
internal sealed record BatchCreateDocumentsApiRequest(
    [property: JsonPropertyName("expected_revision")] int ExpectedRevision,
    [property: JsonPropertyName("items")] List<BatchCreateDocumentItemApiRequest>? Items,
    [property: JsonPropertyName("create_mode")] string? CreateMode);
internal sealed record BatchCreateDocumentItemApiRequest(
    [property: JsonPropertyName("item_id")] string? ItemId,
    [property: JsonPropertyName("expected_item_revision")] int ExpectedItemRevision,
    [property: JsonPropertyName("fact_ids")] List<string>? FactIds,
    [property: JsonPropertyName("metadata_selection")] JsonObject? MetadataSelection);
