using System.Net;
using System.Diagnostics;
using PersonaWorkbench.Web;
using PersonaWorkbench.Core;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
int webPort = ResolvePort(args);
builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, webPort));

WebApplication app = builder.Build();
string workstationInstanceId = "pwb-instance-" + Guid.NewGuid().ToString("N");
WorkbenchSessionManager sessions = new WorkbenchSessionManager();
WorkspaceDocumentService documents = new WorkspaceDocumentService();
ProviderSessionKeyVault providerSessionKeyVault = new ProviderSessionKeyVault();
PersonaContractClosureService contractClosure = new PersonaContractClosureService();
ProviderRequestCapture? providerRequestCapture = ProviderRequestCapture.FromEnvironment();
HttpClientHandler providerHandler = new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false, Proxy = null };
ProviderDraftActionService providerActions = new ProviderDraftActionService(
    providerSessionKeyVault,
    new ProviderDraftClient(providerHandler),
    new ProviderTextExpansionClient(providerHandler, requestCapture: providerRequestCapture),
    new ProviderDslConversionClient(providerHandler, requestCapture: providerRequestCapture));
BatchGenerationService batchGeneration = new BatchGenerationService(providerActions);
app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    context.Response.ContentType = "application/json";
    await context.Response.WriteAsJsonAsync(new
    {
        errorCode = "workbench.internal_error",
        errorMessage = "Workbench could not complete the request."
    });
}));
app.Use(async (context, next) =>
{
    if (!WorkbenchWebPolicy.IsAllowedRequest(context))
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return;
    }

    WorkbenchWebPolicy.ApplySecurityHeaders(context.Response);
    await next();
});
app.UseDefaultFiles();
app.UseStaticFiles();
WorkbenchHealth Health() => WorkbenchHealth.Create(workstationInstanceId, webPort);
WorkbenchHealthRoutes.Map(app, Health);
app.MapPost("/api/session/launch-token", () =>
{
    return Results.Ok(new WorkbenchLaunchTokenResponse { Token = sessions.IssueBootstrapToken() });
});
app.MapPost("/api/session/bootstrap", (WorkbenchBootstrapRequest? request) =>
{
    try
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Token)) return UnauthorizedJson();
        return sessions.TryExchange(request.Token, out WorkbenchSessionGrant grant)
            ? Results.Ok(grant)
            : UnauthorizedJson();
    }
    catch (Exception error) when (builder.Environment.IsDevelopment())
    {
        Console.Error.WriteLine(error);
        throw;
    }
});
app.MapPost("/api/preview", (HttpRequest request, PersonaPreviewRequest previewRequest) =>
{
    if (!IsAuthorized(request)) return UnauthorizedJson();
    PersonaPreviewResponse response = PersonaPreviewService.Build(previewRequest);
    return response.IsValid ? Results.Ok(response) : Results.BadRequest(response);
});
app.MapPost("/api/preview/awake-authoring", (HttpRequest request, AwakeAuthoringPreviewRequest previewRequest) =>
{
    if (!IsAuthorized(request)) return UnauthorizedJson();
    AwakeAuthoringPreviewResponse response = AwakeAuthoringPreviewService.Build(previewRequest);
    return response.IsValid ? Results.Ok(response) : Results.BadRequest(response);
});
app.MapPost("/api/authoring/receipt", (HttpRequest request, ApprovalReceiptRouteRequest routeRequest) => PersonaContractRouteFactory.IssueReceipt(request, routeRequest, sessions, contractClosure));
app.MapPost("/api/authoring/handoff", (HttpRequest request, AuthoringHandoffRouteRequest routeRequest) => PersonaContractRouteFactory.IssueHandoff(request, routeRequest, sessions, contractClosure));
app.MapPost("/api/materials/segment", (MaterialSegmentRequest request) =>
{
    MaterialSegmentResponse response = MaterialImportService.Segment(request);
    return response.IsValid ? Results.Ok(response) : Results.BadRequest(response);
});
app.MapPost("/api/documents/load", (HttpRequest request, WorkspaceDocumentRequest documentRequest) =>
{
    return IsAuthorized(request) ? ToWorkspaceDocumentResult(documents.Load(documentRequest)) : UnauthorizedJson();
});
app.MapPost("/api/documents/save", (HttpRequest request, WorkspaceSaveRequest documentRequest) =>
{
    return IsAuthorized(request) ? ToWorkspaceDocumentResult(documents.Save(documentRequest)) : UnauthorizedJson();
});
app.MapPost("/api/documents/approve", (HttpRequest request, WorkspaceSaveRequest documentRequest) =>
{
    return IsAuthorized(request) ? ToWorkspaceDocumentResult(documents.Approve(documentRequest)) : UnauthorizedJson();
});
app.MapPost("/api/provider/confirm-cloud", (HttpRequest request, ProviderCloudConfirmationRequest confirmationRequest) =>
{
    if (!IsAuthorized(request)) return UnauthorizedJson();
    return providerActions.TryConfirmCloudEndpoint(confirmationRequest.Endpoint, out string errorCode)
        ? Results.NoContent()
        : Results.BadRequest(new { errorCode });
});
app.MapPost("/api/provider/session-key", (HttpRequest request, ProviderSessionKeyRequest keyRequest) =>
{
    if (!IsAuthorized(request)) return UnauthorizedJson();
    return providerActions.TrySetSessionKey(keyRequest.ApiKey, out string errorCode)
        ? Results.NoContent()
        : Results.BadRequest(new { errorCode });
});
app.MapPost("/api/provider/clear-session-key", (HttpRequest request) =>
{
    if (!IsAuthorized(request)) return UnauthorizedJson();
    return providerActions.TryClearSessionKey(out string errorCode)
        ? Results.NoContent()
        : Results.Conflict(new { errorCode });
});
app.MapPost("/api/provider/generate-draft", async (HttpRequest request, ProviderDraftActionRequest providerRequest, CancellationToken cancellationToken) =>
{
    if (!IsAuthorized(request)) return UnauthorizedJson();
    ProviderDraftActionResponse response = await providerActions.GenerateAsync(providerRequest, cancellationToken);
    return response.Status == ProviderDraftActionStatus.Success ? Results.Ok(response) : Results.Conflict(response);
});
app.MapPost("/api/provider/expand-description", async (HttpRequest request, ProviderTextExpansionActionRequest providerRequest, CancellationToken cancellationToken) =>
{
    if (!IsAuthorized(request)) return UnauthorizedJson();
    if (!TryBeginDiagnosticCapture(request, out ProviderRequestCaptureScope? captureScope, out IResult? diagnosticError)) return diagnosticError!;
    using (captureScope)
    {
        ProviderTextExpansionActionResponse response = await providerActions.ExpandDescriptionAsync(providerRequest, cancellationToken);
        return response.Status == ProviderDraftActionStatus.Success ? Results.Ok(response) : Results.Conflict(response);
    }
});
app.MapPost("/api/provider/convert-to-dsl", async (HttpRequest request, ProviderDslConversionActionRequest providerRequest, CancellationToken cancellationToken) =>
{
    if (!IsAuthorized(request)) return UnauthorizedJson();
    if (!TryBeginDiagnosticCapture(request, out ProviderRequestCaptureScope? captureScope, out IResult? diagnosticError)) return diagnosticError!;
    using (captureScope)
    {
        ProviderDslConversionActionResponse response = await providerActions.ConvertToDslAsync(providerRequest, cancellationToken);
        return response.Status switch
        {
            ProviderDraftActionStatus.Success => Results.Ok(response),
            ProviderDraftActionStatus.CanonicalFailure => Results.BadRequest(response),
            _ => Results.Conflict(response)
        };
    }
});
app.MapPost("/api/provider/generate-batch", async (HttpRequest request, BatchGenerationRequest batchRequest, CancellationToken cancellationToken) =>
{
    if (!IsAuthorized(request)) return UnauthorizedJson();
    BatchGenerationResponse response = await batchGeneration.GenerateAsync(batchRequest, cancellationToken);
    return response.Status switch
    {
        BatchGenerationStatuses.Invalid => Results.BadRequest(response),
        BatchGenerationStatuses.Busy => Results.Conflict(response),
        _ => Results.Ok(response)
    };
});
app.MapGet("/api/provider/batch-progress/{batchId}", (HttpRequest request, string batchId) =>
{
    if (!IsAuthorized(request)) return UnauthorizedJson();
    BatchGenerationResponse? response = batchGeneration.GetProgress(batchId);
    return response == null
        ? Results.NotFound(new { errorCode = "batch.not_found" })
        : Results.Ok(response);
});
app.MapPost("/api/provider/cancel-batch", (HttpRequest request, BatchCancelRequest cancelRequest) =>
{
    if (!IsAuthorized(request)) return UnauthorizedJson();
    BatchCancelResponse response = batchGeneration.Cancel(cancelRequest.BatchId);
    return response.IsAccepted ? Results.Ok(response) : Results.Conflict(response);
});
app.MapGet("/api/provider/failures", (HttpRequest request) =>
{
    return IsAuthorized(request) ? Results.Ok(providerActions.GetQuarantinedFailures()) : UnauthorizedJson();
});
app.MapFallbackToFile("index.html");
if (!args.Contains("--no-browser", StringComparer.Ordinal))
{
    string launchUrl = "http://127.0.0.1:" + webPort + "/#" + sessions.BootstrapToken;
    app.Lifetime.ApplicationStarted.Register(() => _ = Process.Start(new ProcessStartInfo(launchUrl) { UseShellExecute = true }));
}
app.Run();

int ResolvePort(string[] arguments)
{
    string? argumentValue = null;
    for (int index = 0; index < arguments.Length - 1; index++)
    {
        if (string.Equals(arguments[index], "--port", StringComparison.Ordinal))
        {
            argumentValue = arguments[index + 1];
            break;
        }
    }

    string? configured = argumentValue ?? Environment.GetEnvironmentVariable("AWAKE_PWB_PORT");
    return int.TryParse(configured, out int parsed)
        && parsed is >= 1024 and <= 65535
        ? parsed
        : WorkbenchWebPolicy.DefaultPort;
}

bool IsAuthorized(HttpRequest request)
{
    return sessions.IsAuthorized(request.Headers["X-Pwb-Session"], request.Headers["X-Pwb-Csrf"]);
}

bool TryBeginDiagnosticCapture(HttpRequest request, out ProviderRequestCaptureScope? scope, out IResult? error)
{
    scope = null;
    error = null;
    if (providerRequestCapture == null) return true;
    string stage = request.Headers["X-Pwb-Diagnostic-Stage"].ToString();
    if (string.IsNullOrWhiteSpace(stage)) return true;
    if (!WorkbenchWebPolicy.IsLoopback(request.HttpContext.Connection.RemoteIpAddress))
    {
        error = Results.BadRequest(new { errorCode = "provider.diagnostic_stage_invalid" });
        return false;
    }
    try
    {
        scope = providerRequestCapture.BeginScope(stage);
        return true;
    }
    catch (ArgumentException)
    {
        error = Results.BadRequest(new { errorCode = "provider.diagnostic_stage_invalid" });
        return false;
    }
}

IResult UnauthorizedJson()
{
    return Results.Json(new
    {
        errorCode = "session.unauthorized",
        errorMessage = "Workbench session is missing or expired."
    }, statusCode: StatusCodes.Status401Unauthorized);
}

IResult ToWorkspaceDocumentResult(WorkspaceDocumentResponse response)
{
    if (response.IsSuccess) return Results.Ok(response);

    int statusCode = response.ErrorCode switch
    {
        "workspace.document_not_found" => StatusCodes.Status404NotFound,
        "workspace.conflict" or "workspace.locked" => StatusCodes.Status409Conflict,
        "workspace.access_denied" => StatusCodes.Status403Forbidden,
        _ when response.ErrorCode.StartsWith("persona.", StringComparison.Ordinal)
            || response.ErrorCode.StartsWith("tag.", StringComparison.Ordinal)
            || response.ErrorCode.StartsWith("facet.", StringComparison.Ordinal)
            || response.ErrorCode.StartsWith("axis.", StringComparison.Ordinal)
            || response.ErrorCode is "workspace.request_invalid"
                or "workspace.root_required"
                or "workspace.root_invalid"
                or "workspace.file_name_invalid"
                or "workspace.document_required"
                or "workspace.path_invalid"
                or "workspace.path_outside" => StatusCodes.Status400BadRequest,
        _ => StatusCodes.Status500InternalServerError
    };

    return Results.Json(response, statusCode: statusCode);
}

public sealed class WorkbenchLaunchTokenResponse
{
    public string Token { get; set; } = string.Empty;
}

public sealed class WorkbenchBootstrapRequest
{
    public string Token { get; set; } = string.Empty;
}

public sealed class ProviderCloudConfirmationRequest
{
    public string Endpoint { get; set; } = string.Empty;
}

public sealed class ProviderSessionKeyRequest
{
    public string ApiKey { get; set; } = string.Empty;
}





