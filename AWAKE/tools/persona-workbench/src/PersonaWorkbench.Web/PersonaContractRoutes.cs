using System.Text.Json;
using PersonaWorkbench.Core;

namespace PersonaWorkbench.Web;

public sealed class ApprovalReceiptRouteRequest
{
    public PersonaDocument? Document { get; set; }
    public string RequestId { get; set; } = string.Empty;
    public string EvidenceId { get; set; } = string.Empty;
    public long Fence { get; set; }
    public int LifetimeSeconds { get; set; } = 1800;
}

public sealed class AuthoringHandoffRouteRequest
{
    public ApprovalReceipt? Receipt { get; set; }
    public PersonaDocument? Document { get; set; }
    public string WorkspaceId { get; set; } = string.Empty;
    public long Revision { get; set; }
    public string RequestId { get; set; } = string.Empty;
    public long Fence { get; set; }
    public int LifetimeSeconds { get; set; } = 1800;
}

public static class PersonaContractRouteFactory
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static IResult IssueReceipt(HttpRequest httpRequest, ApprovalReceiptRouteRequest request, WorkbenchSessionManager sessions, PersonaContractClosureService closure)
    {
        if (request == null || request.Document == null || string.IsNullOrWhiteSpace(request.RequestId)) return BadRequest("receipt.request_invalid");
        string operationFingerprint = PersonaContractClosureValidator.RequestFingerprint(request.RequestId, request.EvidenceId + "\n" + JsonSerializer.Serialize(request.Document, JsonOptions));
        if (!sessions.TryAuthorizeFence(httpRequest.Headers["X-Pwb-Session"], httpRequest.Headers["X-Pwb-Csrf"], request.Fence, operationFingerprint, out string sessionId, out string issuerId, out string errorCode)) return SessionFailure(errorCode);
        ContractClosureResult<ApprovalReceipt> result = closure.IssueReceipt(request.Document, new ContractIssuerContext { SessionId = sessionId, IssuerId = issuerId, Fence = request.Fence }, request.RequestId, request.EvidenceId, TimeSpan.FromSeconds(request.LifetimeSeconds));
        return result.IsSuccess ? Results.Ok(result.Value) : BadRequest(result.Errors.ToArray());
    }

    public static IResult IssueHandoff(HttpRequest httpRequest, AuthoringHandoffRouteRequest request, WorkbenchSessionManager sessions, PersonaContractClosureService closure)
    {
        if (request == null || request.Receipt == null || request.Document == null || string.IsNullOrWhiteSpace(request.RequestId)
            || !PersonaContractClosureValidator.IsValidWorkspaceId(request.WorkspaceId) || request.Revision < 1)
            return BadRequest("handoff.request_invalid");
        string operationFingerprint = PersonaContractClosureValidator.RequestFingerprint(request.RequestId, request.Receipt.ReceiptId + "\n" + request.WorkspaceId + "\n" + request.Revision + "\n" + JsonSerializer.Serialize(request.Document, JsonOptions));
        if (!sessions.TryAuthorizeFence(httpRequest.Headers["X-Pwb-Session"], httpRequest.Headers["X-Pwb-Csrf"], request.Fence, operationFingerprint, out string sessionId, out string issuerId, out string errorCode)) return SessionFailure(errorCode);
        ContractClosureResult<AuthoringHandoff> result = closure.IssueHandoff(request.Receipt, request.Document, new ContractIssuerContext { SessionId = sessionId, IssuerId = issuerId, Fence = request.Fence }, new AuthoringHandoffEnvelopeContext { WorkspaceId = request.WorkspaceId, Revision = request.Revision }, request.RequestId, TimeSpan.FromSeconds(request.LifetimeSeconds));
        return result.IsSuccess ? Results.Ok(result.Value) : BadRequest(result.Errors.ToArray());
    }

    private static IResult SessionFailure(string errorCode) => errorCode switch
    {
        "session.unauthorized" => Results.Json(new { errorCode }, statusCode: StatusCodes.Status401Unauthorized),
        "session.fence_old" or "session.replay_conflict" => Results.Conflict(new { errorCode }),
        _ => Results.BadRequest(new { errorCode })
    };

    private static IResult BadRequest(string errorCode) => Results.BadRequest(new { errorCode });
    private static IResult BadRequest(string[] errors) => Results.BadRequest(new { errorCode = "contract.invalid", errors });
}
